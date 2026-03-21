using System.Diagnostics;
using WhisperHeim.Models;
using WhisperHeim.Services.Hotkey;
using WhisperHeim.Services.Settings;
using WhisperHeim.Services.TextToSpeech;

namespace WhisperHeim.Services.SelectedText;

/// <summary>
/// Registers a global hotkey that captures selected text from any application
/// and reads it aloud using the text-to-speech engine.
/// Default hotkey: Ctrl + Shift + R (user-configurable via TTS settings).
/// </summary>
public sealed class ReadAloudHotkeyService : IDisposable
{
    /// <summary>
    /// Default read-aloud hotkey: Ctrl + Shift + R.
    /// </summary>
    public static readonly HotkeyRegistration DefaultHotkey = new(
        ModifierKeys.Control | ModifierKeys.Shift,
        VirtualKey: 0x52 // 'R' key
    );

    private readonly ISelectedTextService _selectedTextService;
    private readonly ITextToSpeechService _textToSpeechService;
    private readonly SettingsService _settingsService;
    private readonly GlobalHotkeyService _hotkeyService = new();
    private CancellationTokenSource? _currentReadCts;
    private bool _disposed;

    /// <summary>
    /// The default voice ID to use for reading aloud.
    /// Can be updated by the UI when the user selects a different voice.
    /// Falls back to settings if not explicitly set.
    /// </summary>
    public string? VoiceId { get; set; }

    /// <summary>
    /// Speech speed multiplier (1.0 = normal).
    /// </summary>
    public float Speed { get; set; } = 1.0f;

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
        ITextToSpeechService textToSpeechService,
        SettingsService settingsService)
    {
        _selectedTextService = selectedTextService ?? throw new ArgumentNullException(nameof(selectedTextService));
        _textToSpeechService = textToSpeechService ?? throw new ArgumentNullException(nameof(textToSpeechService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    /// <summary>
    /// Registers the read-aloud hotkey. Reads the hotkey from settings if available,
    /// otherwise uses the provided hotkey or the default (Ctrl+Shift+R).
    /// </summary>
    public bool Register(HotkeyRegistration? hotkey = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Resolve hotkey: explicit param > settings > default
        var resolvedHotkey = hotkey
            ?? HotkeyRegistration.TryParse(TtsSettings.ReadAloudHotkey)
            ?? DefaultHotkey;

        // Apply voice from settings if not explicitly set
        if (VoiceId is null && TtsSettings.DefaultVoiceId is not null)
            VoiceId = TtsSettings.DefaultVoiceId;

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

        // Refresh voice from settings
        VoiceId = TtsSettings.DefaultVoiceId;

        return Register();
    }

    /// <summary>
    /// Stops any currently playing read-aloud speech.
    /// </summary>
    public void StopReading()
    {
        _currentReadCts?.Cancel();
        _currentReadCts = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _currentReadCts?.Cancel();
        _currentReadCts?.Dispose();
        _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
        _hotkeyService.Dispose();
    }

    private async void OnHotkeyPressed(object? sender, EventArgs e)
    {
        try
        {
            // Cancel any ongoing read-aloud
            _currentReadCts?.Cancel();
            _currentReadCts?.Dispose();
            _currentReadCts = new CancellationTokenSource();
            var ct = _currentReadCts.Token;

            // Capture selected text
            var text = await _selectedTextService.CaptureSelectedTextAsync(ct);
            if (string.IsNullOrWhiteSpace(text))
            {
                Trace.TraceInformation("[ReadAloudHotkeyService] No text selected, nothing to read");
                return;
            }

            // Ensure TTS model is loaded
            if (!_textToSpeechService.IsLoaded)
            {
                Trace.TraceInformation("[ReadAloudHotkeyService] Loading TTS model...");
                _textToSpeechService.LoadModel();
            }

            // Determine voice to use: explicit VoiceId > settings > first available
            var voiceId = VoiceId ?? TtsSettings.DefaultVoiceId;
            if (string.IsNullOrEmpty(voiceId))
            {
                var voices = _textToSpeechService.GetAvailableVoices();
                voiceId = voices.Count > 0 ? voices[0].Id : null;
            }

            if (voiceId == null)
            {
                Trace.TraceWarning("[ReadAloudHotkeyService] No voices available for TTS");
                return;
            }

            // Resolve playback device from settings
            var deviceNumber = -1; // system default
            if (int.TryParse(TtsSettings.PlaybackDeviceId, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsedDevice))
            {
                deviceNumber = parsedDevice;
            }

            Trace.TraceInformation("[ReadAloudHotkeyService] Reading aloud: \"{0}\" (voice={1}, speed={2}, device={3})",
                text.Length > 50 ? text[..50] + "..." : text, voiceId, Speed, deviceNumber);

            await _textToSpeechService.SpeakAsync(text, voiceId, Speed, deviceNumber, ct);
        }
        catch (OperationCanceledException)
        {
            Trace.TraceInformation("[ReadAloudHotkeyService] Read-aloud cancelled");
        }
        catch (Exception ex)
        {
            Trace.TraceError("[ReadAloudHotkeyService] Error during read-aloud: {0}", ex);
        }
    }
}
