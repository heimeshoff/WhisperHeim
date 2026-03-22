using System.Diagnostics;
using System.Windows;
using WhisperHeim.Services.Dictation;
using WhisperHeim.Services.Hotkey;
using WhisperHeim.Services.Input;

namespace WhisperHeim.Services.Templates;

/// <summary>
/// Orchestrates the template workflow:
/// 1. User presses the template hotkey
/// 2. Dictation starts to capture a short spoken template name
/// 3. After speech ends (or a timeout), the transcribed text is fuzzy-matched
///    against template names
/// 4. The matched template's expanded text is typed into the active window
/// 5. A confirmation toast is shown
///
/// Uses a separate <see cref="GlobalHotkeyService"/> instance so the template
/// hotkey (Alt+Win) is independent from the dictation hotkey (Ctrl+Win).
/// </summary>
public sealed class TemplateOrchestrator : IDisposable
{
    private readonly GlobalHotkeyService _hotkeyService;
    private readonly IDictationPipeline _pipeline;
    private readonly IInputSimulator _inputSimulator;
    private readonly ITemplateService _templateService;
    private readonly Action<string> _showNotification;

    private readonly object _lock = new();
    private bool _isListening;
    private bool _disposed;
    private string _accumulatedText = string.Empty;
    private Timer? _timeoutTimer;

    /// <summary>
    /// Maximum time (ms) to listen for a template name after hotkey press.
    /// </summary>
    private const int ListenTimeoutMs = 4000;

    public TemplateOrchestrator(
        GlobalHotkeyService hotkeyService,
        IDictationPipeline pipeline,
        IInputSimulator inputSimulator,
        ITemplateService templateService,
        Action<string> showNotification)
    {
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _inputSimulator = inputSimulator ?? throw new ArgumentNullException(nameof(inputSimulator));
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _showNotification = showNotification ?? throw new ArgumentNullException(nameof(showNotification));
    }

    /// <summary>
    /// Starts listening for the template hotkey.
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        Trace.TraceInformation("[TemplateOrchestrator] Started. Listening for template hotkey.");
    }

    /// <summary>
    /// Stops listening.
    /// </summary>
    public void Stop()
    {
        _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
        StopListening();
        Trace.TraceInformation("[TemplateOrchestrator] Stopped.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            if (_isListening)
            {
                // Second press while listening -> cancel
                Trace.TraceInformation("[TemplateOrchestrator] Hotkey pressed while listening, cancelling.");
                StopListening();
                return;
            }

            _isListening = true;
            _accumulatedText = string.Empty;
        }

        Trace.TraceInformation("[TemplateOrchestrator] Template hotkey pressed, starting voice capture...");

        // Wire pipeline events for template name capture
        _pipeline.FinalResult += OnFinalResult;
        _pipeline.Error += OnPipelineError;

        // Start dictation to capture the spoken template name
        try
        {
            _pipeline.Start();
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TemplateOrchestrator] Failed to start pipeline: {0}", ex.Message);
            CleanupPipelineEvents();
            lock (_lock) { _isListening = false; }
            NotifyUser("Template: Failed to start voice capture.");
            return;
        }

        // Set a timeout in case the user doesn't speak
        _timeoutTimer = new Timer(OnTimeout, null, ListenTimeoutMs, Timeout.Infinite);
    }

    private void OnFinalResult(object? sender, DictationResultEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Text))
            return;

        string spokenText;
        lock (_lock)
        {
            if (!_isListening) return;
            _accumulatedText += (string.IsNullOrEmpty(_accumulatedText) ? "" : " ") + e.Text.Trim();
            spokenText = _accumulatedText;
        }

        Trace.TraceInformation("[TemplateOrchestrator] Captured spoken text: \"{0}\"", spokenText);

        // Stop listening and process the match
        StopListening();
        ProcessTemplateMatch(spokenText);
    }

    private void OnPipelineError(object? sender, DictationErrorEventArgs e)
    {
        Trace.TraceError("[TemplateOrchestrator] Pipeline error: {0}", e.Message);
        StopListening();
        NotifyUser("Template: Voice capture error.");
    }

    private void OnTimeout(object? state)
    {
        string spokenText;
        lock (_lock)
        {
            if (!_isListening) return;
            spokenText = _accumulatedText;
        }

        Trace.TraceInformation("[TemplateOrchestrator] Timeout reached. Accumulated: \"{0}\"", spokenText);
        StopListening();

        if (!string.IsNullOrWhiteSpace(spokenText))
        {
            ProcessTemplateMatch(spokenText);
        }
        else
        {
            NotifyUser("Template: No speech detected.");
        }
    }

    private void ProcessTemplateMatch(string spokenText)
    {
        var result = _templateService.MatchAndExpand(spokenText);

        if (result is null)
        {
            NotifyUser($"Template: No match for \"{spokenText}\"");
            return;
        }

        Trace.TraceInformation(
            "[TemplateOrchestrator] Matched template \"{0}\", inserting text.", result.TemplateName);

        // Type the expanded template text
        _ = TypeTemplateSafeAsync(result.ExpandedText);

        NotifyUser($"Template: {result.TemplateName}");
    }

    private async Task TypeTemplateSafeAsync(string text)
    {
        try
        {
            await _inputSimulator.TypeTextAsync(text);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TemplateOrchestrator] Failed to type template text: {0}", ex.Message);
        }
    }

    private void StopListening()
    {
        lock (_lock)
        {
            if (!_isListening) return;
            _isListening = false;
        }

        _timeoutTimer?.Dispose();
        _timeoutTimer = null;

        // Stop pipeline if running
        try
        {
            if (_pipeline.IsRunning)
                _pipeline.Stop();
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[TemplateOrchestrator] Error stopping pipeline: {0}", ex.Message);
        }

        CleanupPipelineEvents();
    }

    private void CleanupPipelineEvents()
    {
        _pipeline.FinalResult -= OnFinalResult;
        _pipeline.Error -= OnPipelineError;
    }

    private void NotifyUser(string message)
    {
        try
        {
            Application.Current?.Dispatcher?.BeginInvoke(() =>
            {
                try
                {
                    _showNotification(message);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("[TemplateOrchestrator] Notification error: {0}", ex.Message);
                }
            });
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TemplateOrchestrator] Failed to dispatch notification: {0}", ex.Message);
        }
    }
}
