using WhisperHeim.Services.Hotkey;

namespace WhisperHeim.Services.Recording;

/// <summary>
/// Manages a dedicated global hotkey for toggling call recording (Ctrl+Shift+Win+R by default).
/// Delegates to <see cref="GlobalHotkeyService"/> which uses a low-level keyboard hook.
/// </summary>
public sealed class CallRecordingHotkeyService : IDisposable
{
    /// <summary>
    /// Default call recording hotkey: Ctrl + Shift + Win + R.
    /// </summary>
    public static readonly HotkeyRegistration DefaultHotkey = new(
        ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Win,
        VirtualKey: 0x52 // 'R' key
    );

    private readonly ICallRecordingService _recordingService;
    private readonly GlobalHotkeyService _hotkeyService = new();
    private bool _disposed;

    public CallRecordingHotkeyService(ICallRecordingService recordingService)
    {
        _recordingService = recordingService ?? throw new ArgumentNullException(nameof(recordingService));
    }

    /// <summary>
    /// The currently configured hotkey combination for call recording.
    /// </summary>
    public HotkeyRegistration Hotkey => _hotkeyService.Hotkey;

    /// <summary>
    /// Registers the call recording hotkey.
    /// </summary>
    public bool Register(HotkeyRegistration? hotkey = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        return _hotkeyService.Register(hotkey: hotkey ?? DefaultHotkey);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
        _hotkeyService.Dispose();
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        _recordingService.ToggleRecording();
    }
}
