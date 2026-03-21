using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WhisperHeim.Services.Hotkey;

/// <summary>
/// Manages a global hotkey using Win32 RegisterHotKey / UnregisterHotKey.
/// Raises <see cref="HotkeyPressed"/> when the registered hotkey is pressed from any application.
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x7701; // arbitrary unique id within the app

    private HwndSource? _hwndSource;
    private IntPtr _windowHandle;
    private bool _registered;
    private bool _disposed;

    /// <summary>
    /// Raised when the registered global hotkey is pressed.
    /// </summary>
    public event EventHandler? HotkeyPressed;

    /// <summary>
    /// The currently configured hotkey combination.
    /// </summary>
    public HotkeyRegistration Hotkey { get; private set; } = HotkeyRegistration.Default;

    /// <summary>
    /// Registers the global hotkey using the given WPF window as the message sink.
    /// Call this once during application startup (after the window handle is available).
    /// </summary>
    /// <param name="window">A WPF window whose HWND will receive WM_HOTKEY messages.</param>
    /// <param name="hotkey">
    /// Optional hotkey override. When <c>null</c>, <see cref="HotkeyRegistration.Default"/> is used.
    /// </param>
    /// <returns>
    /// <c>true</c> if the hotkey was registered successfully;
    /// <c>false</c> if registration failed (e.g., another app already owns the combination).
    /// </returns>
    public bool Register(Window window, HotkeyRegistration? hotkey = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_registered)
            Unregister();

        Hotkey = hotkey ?? HotkeyRegistration.Default;

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
            int error = Marshal.GetLastWin32Error();
            System.Diagnostics.Debug.WriteLine(
                $"[GlobalHotkeyService] RegisterHotKey failed – Win32 error {error} " +
                $"(0x{error:X8}). The hotkey may be registered by another application."
            );
            // Clean up the hook since we failed
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
    /// Safe to call multiple times.
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

    // ── private ────────────────────────────────────────────────────

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            OnHotkeyPressed();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void OnHotkeyPressed()
    {
        HotkeyPressed?.Invoke(this, EventArgs.Empty);
    }
}
