using System.Windows;
using System.Windows.Interop;
using WhisperHeim.Services.Hotkey;

namespace WhisperHeim.Services.Recording;

/// <summary>
/// Manages a dedicated global hotkey for toggling call recording (Ctrl+Shift+Win by default).
/// This is separate from the existing GlobalHotkeyService which handles the dictation hotkey.
/// </summary>
public sealed class CallRecordingHotkeyService : IDisposable
{
    private const int HotkeyId = 0x7702; // distinct from GlobalHotkeyService's 0x7701

    /// <summary>
    /// Default call recording hotkey: Ctrl + Shift + Win.
    /// The VK is 0x52 ('R') to form Ctrl+Shift+Win+R.
    /// </summary>
    public static readonly HotkeyRegistration DefaultHotkey = new(
        ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Win,
        VirtualKey: 0x52 // 'R' key
    );

    private readonly ICallRecordingService _recordingService;
    private HwndSource? _hwndSource;
    private IntPtr _windowHandle;
    private bool _registered;
    private bool _disposed;

    public CallRecordingHotkeyService(ICallRecordingService recordingService)
    {
        _recordingService = recordingService ?? throw new ArgumentNullException(nameof(recordingService));
    }

    /// <summary>
    /// The currently configured hotkey combination for call recording.
    /// </summary>
    public HotkeyRegistration Hotkey { get; private set; } = DefaultHotkey;

    /// <summary>
    /// Registers the call recording hotkey using the given WPF window as the message sink.
    /// </summary>
    /// <param name="window">A WPF window whose HWND will receive WM_HOTKEY messages.</param>
    /// <param name="hotkey">
    /// Optional hotkey override. When null, the default Ctrl+Shift+Win+R is used.
    /// </param>
    /// <returns>True if registration succeeded; false if the combination is already taken.</returns>
    public bool Register(Window window, HotkeyRegistration? hotkey = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_registered)
            Unregister();

        Hotkey = hotkey ?? DefaultHotkey;

        var helper = new WindowInteropHelper(window);
        _windowHandle = helper.EnsureHandle();
        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        _hwndSource?.AddHook(WndProc);

        bool success = NativeMethods.RegisterHotKey(
            _windowHandle,
            HotkeyId,
            (uint)Hotkey.Modifiers,
            (uint)Hotkey.VirtualKey
        );

        if (!success)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[CallRecordingHotkeyService] RegisterHotKey failed. " +
                "The hotkey may be registered by another application.");
            _hwndSource?.RemoveHook(WndProc);
            _hwndSource = null;
            _windowHandle = IntPtr.Zero;
            return false;
        }

        _registered = true;
        return true;
    }

    /// <summary>
    /// Unregisters the hotkey and detaches from the window message loop.
    /// </summary>
    public void Unregister()
    {
        if (!_registered)
            return;

        NativeMethods.UnregisterHotKey(_windowHandle, HotkeyId);
        _hwndSource?.RemoveHook(WndProc);
        _hwndSource = null;
        _windowHandle = IntPtr.Zero;
        _registered = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unregister();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            _recordingService.ToggleRecording();
            handled = true;
        }

        return IntPtr.Zero;
    }
}
