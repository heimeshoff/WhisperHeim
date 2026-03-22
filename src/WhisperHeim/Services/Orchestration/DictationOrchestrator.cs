using System.Diagnostics;
using System.Windows;
using WhisperHeim.Services.Audio;
using WhisperHeim.Services.Hotkey;
using WhisperHeim.Services.Input;
using WhisperHeim.Services.Templates;
using WhisperHeim.Services.Transcription;

namespace WhisperHeim.Services.Orchestration;

/// <summary>
/// Hold-to-talk dictation orchestrator.
///
/// Key down: start recording + show overlay.
/// While holding: accumulate audio samples.
/// Key up: stop recording, transcribe full audio, type the final result.
///
/// Template mode: if Alt is held during recording (Ctrl+Win+Alt), the transcribed
/// text is fuzzy-matched against templates. If a match is found, the template's
/// expanded text is typed instead. If no match, the raw transcription is typed.
///
/// No VAD needed -- the user controls speech boundaries by holding/releasing the hotkey.
/// </summary>
public sealed class DictationOrchestrator : IDisposable
{
    private readonly GlobalHotkeyService _hotkeyService;
    private readonly IAudioCaptureService _audioCapture;
    private readonly ITranscriptionService _transcription;
    private readonly IInputSimulator _inputSimulator;
    private readonly ITemplateService? _templateService;
    private readonly Action<bool> _onDictationStateChanged;

    private readonly object _lock = new();
    private readonly List<float> _recordedSamples = new();
    private bool _isRecording;
    private bool _isTemplateMode;
    private bool _disposed;

    private const int MinSamples = 8000; // 0.5s at 16kHz
    private const int SampleRate = 16000;

    /// <summary>
    /// Raised on a background thread with the RMS amplitude of each audio chunk.
    /// Value is in [0.0, 1.0] range.
    /// </summary>
    public event Action<double>? AudioAmplitudeChanged;

    /// <summary>
    /// Raised on a background thread when a pipeline error occurs.
    /// </summary>
    public event Action<Exception>? PipelineError;

    /// <summary>
    /// Raised when template mode is active but no template matched the spoken text.
    /// The string parameter contains the transcribed text that failed to match.
    /// </summary>
    public event Action<string>? TemplateNoMatch;

    public DictationOrchestrator(
        GlobalHotkeyService hotkeyService,
        IAudioCaptureService audioCapture,
        ITranscriptionService transcription,
        IInputSimulator inputSimulator,
        Action<bool> onDictationStateChanged,
        ITemplateService? templateService = null)
    {
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
        _audioCapture = audioCapture ?? throw new ArgumentNullException(nameof(audioCapture));
        _transcription = transcription ?? throw new ArgumentNullException(nameof(transcription));
        _inputSimulator = inputSimulator ?? throw new ArgumentNullException(nameof(inputSimulator));
        _onDictationStateChanged = onDictationStateChanged ?? throw new ArgumentNullException(nameof(onDictationStateChanged));
        _templateService = templateService;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        _hotkeyService.HotkeyReleased += OnHotkeyReleased;

        Trace.TraceInformation("[DictationOrchestrator] Started. Hold hotkey to dictate. Hold Alt additionally for template mode.");
    }

    public void Stop()
    {
        _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
        _hotkeyService.HotkeyReleased -= OnHotkeyReleased;

        if (_isRecording)
            StopRecording(transcribe: false);

        Trace.TraceInformation("[DictationOrchestrator] Stopped.");
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
            if (_isRecording) return; // already recording
            _isRecording = true;
            _isTemplateMode = false;
            _recordedSamples.Clear();
        }

        Trace.TraceInformation("[DictationOrchestrator] Hotkey pressed -- starting recording.");

        _audioCapture.AudioDataAvailable += OnAudioData;

        try
        {
            _audioCapture.StartCapture();
        }
        catch (Exception ex)
        {
            Trace.TraceError("[DictationOrchestrator] Failed to start capture: {0}", ex.Message);
            _audioCapture.AudioDataAvailable -= OnAudioData;
            lock (_lock) _isRecording = false;
            PipelineError?.Invoke(ex);
            return;
        }

        NotifyStateChanged(true);
    }

    private void OnHotkeyReleased(object? sender, EventArgs e)
    {
        bool wasRecording;
        lock (_lock)
        {
            wasRecording = _isRecording;
            if (!wasRecording) return;
        }

        Trace.TraceInformation("[DictationOrchestrator] Hotkey released -- stopping and transcribing.");
        StopRecording(transcribe: true);
    }

    private void StopRecording(bool transcribe)
    {
        bool templateMode;
        lock (_lock)
        {
            if (!_isRecording) return;
            _isRecording = false;
            templateMode = _isTemplateMode;
        }

        // Stop capture
        _audioCapture.AudioDataAvailable -= OnAudioData;
        try { _audioCapture.StopCapture(); }
        catch (Exception ex) { Trace.TraceWarning("[DictationOrchestrator] Error stopping capture: {0}", ex.Message); }

        NotifyStateChanged(false);

        if (transcribe)
        {
            float[] samples;
            lock (_lock)
            {
                samples = _recordedSamples.ToArray();
                _recordedSamples.Clear();
            }

            if (samples.Length > MinSamples)
            {
                _ = TranscribeFinalAsync(samples, templateMode);
            }
            else
            {
                Trace.TraceInformation("[DictationOrchestrator] Recording too short ({0} samples), skipping.", samples.Length);
            }
        }
    }

    private void OnAudioData(object? sender, AudioDataEventArgs e)
    {
        try
        {
            lock (_lock)
            {
                if (_isRecording)
                {
                    _recordedSamples.AddRange(e.Samples);

                    // Continuously check if Alt is held during recording.
                    // Once detected, template mode stays on for the rest of this session.
                    if (!_isTemplateMode && _templateService is not null && IsKeyDown(NativeMethods.VK_MENU))
                    {
                        _isTemplateMode = true;
                        Trace.TraceInformation("[DictationOrchestrator] Alt detected during recording -- template mode.");
                    }
                }
            }

            // Calculate RMS amplitude and notify listeners
            var rms = CalculateRms(e.Samples);
            AudioAmplitudeChanged?.Invoke(rms);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[DictationOrchestrator] OnAudioData error: {0}", ex);
        }
    }

    /// <summary>
    /// Calculates the Root Mean Square (RMS) amplitude of audio samples.
    /// Returns a value in [0.0, 1.0] for normalized float samples.
    /// </summary>
    private static double CalculateRms(float[] samples)
    {
        if (samples.Length == 0) return 0.0;

        double sumSquares = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            sumSquares += samples[i] * (double)samples[i];
        }

        return Math.Sqrt(sumSquares / samples.Length);
    }

    private async Task TranscribeFinalAsync(float[] samples, bool templateMode)
    {
        try
        {
            var result = await _transcription.TranscribeAsync(samples, SampleRate);
            var text = result.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            Trace.TraceInformation(
                "[DictationOrchestrator] Final: \"{0}\" (audio={1:F2}s, transcribe={2:F0}ms, RTF={3:F3}, template={4})",
                text, result.AudioDuration.TotalSeconds,
                result.TranscriptionDuration.TotalMilliseconds, result.RealTimeFactor, templateMode);

            if (templateMode && _templateService is not null)
            {
                var match = _templateService.MatchAndExpand(text);
                if (match is not null)
                {
                    Trace.TraceInformation(
                        "[DictationOrchestrator] Template matched: \"{0}\" (score={1:F2}), typing expanded text.",
                        match.TemplateName, match.MatchScore);
                    await TypeTextSafe(match.ExpandedText);
                    return;
                }

                Trace.TraceInformation(
                    "[DictationOrchestrator] No template match for \"{0}\".", text);
                TemplateNoMatch?.Invoke(text);
                return;
            }

            await TypeTextSafe(text);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[DictationOrchestrator] Final transcription error: {0}", ex.Message);
            PipelineError?.Invoke(ex);
        }
    }

    private async Task TypeTextSafe(string text)
    {
        try
        {
            await _inputSimulator.TypeTextAsync(text);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[DictationOrchestrator] Failed to type text: {0}", ex.Message);
        }
    }

    private void NotifyStateChanged(bool isActive)
    {
        try
        {
            Application.Current?.Dispatcher?.BeginInvoke(() =>
            {
                try { _onDictationStateChanged(isActive); }
                catch (Exception ex) { Trace.TraceError("[DictationOrchestrator] State callback error: {0}", ex.Message); }
            });
        }
        catch (Exception ex)
        {
            Trace.TraceError("[DictationOrchestrator] Failed to dispatch state change: {0}", ex.Message);
        }
    }

    private static bool IsKeyDown(int vk) =>
        (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;
}
