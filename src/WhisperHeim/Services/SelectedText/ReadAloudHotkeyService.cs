using System.Diagnostics;
using WhisperHeim.Models;
using WhisperHeim.Services.Hotkey;
using WhisperHeim.Services.Settings;

namespace WhisperHeim.Services.SelectedText;

/// <summary>
/// Registers a global hotkey that captures selected text from any application,
/// then signals the main window to navigate to the TTS page and paste the text.
/// Default hotkey: Ctrl + Win + T (user-configurable via TTS settings).
/// </summary>
public sealed class ReadAloudHotkeyService : IDisposable
{
    /// <summary>
    /// Default read-aloud hotkey: Ctrl + Win + T.
    /// Avoids Ctrl+Alt which triggers AltGr on German keyboards.
    /// </summary>
    public static readonly HotkeyRegistration DefaultHotkey = new(
        ModifierKeys.Control | ModifierKeys.Win,
        VirtualKey: 0x54 // VK_T — T key
    );

    private readonly ISelectedTextService _selectedTextService;
    private readonly SettingsService _settingsService;
    private readonly GlobalHotkeyService _hotkeyService = new();
    private bool _disposed;

    /// <summary>
    /// Raised when the hotkey is pressed and selected text has been captured.
    /// The event argument contains the captured text.
    /// The subscriber should bring the window to the foreground, navigate to the
    /// TTS page, and paste the text into the input workspace.
    /// </summary>
    public event EventHandler<ReadAloudTextCapturedEventArgs>? TextCaptured;

    /// <summary>
    /// The currently configured hotkey combination.
    /// </summary>
    public HotkeyRegistration Hotkey => _hotkeyService.Hotkey;

    /// <summary>
    /// Convenience accessor for the TTS settings section.
    /// </summary>
    private TtsSettings TtsSettings => _settingsService.Current.Tts;

    public ReadAloudHotkeyService(
        ISelectedTextService selectedTextService,
        SettingsService settingsService)
    {
        _selectedTextService = selectedTextService ?? throw new ArgumentNullException(nameof(selectedTextService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    /// <summary>
    /// Registers the read-aloud hotkey. Reads the hotkey from settings if available,
    /// otherwise uses the provided hotkey or the default (Ctrl+Win+Ä).
    /// </summary>
    public bool Register(HotkeyRegistration? hotkey = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Resolve hotkey: explicit param > settings > default
        var resolvedHotkey = hotkey
            ?? HotkeyRegistration.TryParse(TtsSettings.ReadAloudHotkey)
            ?? DefaultHotkey;

        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        return _hotkeyService.Register(hotkey: resolvedHotkey);
    }

    /// <summary>
    /// Re-registers the hotkey with the current settings. Call this after settings change.
    /// </summary>
    public bool ReRegisterFromSettings()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
        _hotkeyService.Unregister();

        return Register();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
        _hotkeyService.Dispose();
    }

    private async void OnHotkeyPressed(object? sender, EventArgs e)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            // Capture selected text from the active application
            var text = await _selectedTextService.CaptureSelectedTextAsync(cts.Token);
            if (string.IsNullOrWhiteSpace(text))
            {
                Trace.TraceInformation("[ReadAloudHotkeyService] No text selected, nothing to do");
                return;
            }

            Trace.TraceInformation("[ReadAloudHotkeyService] Captured text ({0} chars), signaling main window",
                text.Length);

            TextCaptured?.Invoke(this, new ReadAloudTextCapturedEventArgs(text));
        }
        catch (OperationCanceledException)
        {
            Trace.TraceInformation("[ReadAloudHotkeyService] Text capture timed out");
        }
        catch (Exception ex)
        {
            Trace.TraceError("[ReadAloudHotkeyService] Error during text capture: {0}", ex);
        }
    }
}

/// <summary>
/// Event arguments for <see cref="ReadAloudHotkeyService.TextCaptured"/>.
/// </summary>
public sealed class ReadAloudTextCapturedEventArgs : EventArgs
{
    public string Text { get; }

    public ReadAloudTextCapturedEventArgs(string text)
    {
        Text = text;
    }
}
