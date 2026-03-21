using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WhisperHeim.Services.Hotkey;

/// <summary>
/// Manages a global hotkey using a low-level keyboard hook (WH_KEYBOARD_LL).
/// This approach works with Win key combos (unlike RegisterHotKey which Windows intercepts).
/// Raises <see cref="HotkeyPressed"/> when the configured key combination is pressed.
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private NativeMethods.LowLevelKeyboardProc? _hookProc; // prevent GC
    private bool _disposed;
    private bool _targetKeyDown; // tracks key-down state to suppress auto-repeat

    /// <summary>
    /// Raised when the registered global hotkey is pressed (key down).
    /// </summary>
    public event EventHandler? HotkeyPressed;

    /// <summary>
    /// Raised when the registered global hotkey is released (key up).
    /// </summary>
    public event EventHandler? HotkeyReleased;

    /// <summary>
    /// The currently configured hotkey combination.
    /// </summary>
    public HotkeyRegistration Hotkey { get; private set; } = HotkeyRegistration.Default;

    /// <summary>
    /// Installs the low-level keyboard hook with the default or specified hotkey.
    /// The <paramref name="window"/> parameter is accepted for API compatibility but not used
    /// (low-level hooks don't need a window handle).
    /// </summary>
    public bool Register(System.Windows.Window? window = null, HotkeyRegistration? hotkey = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_hookId != IntPtr.Zero)
            Unregister();

        Hotkey = hotkey ?? HotkeyRegistration.Default;

        _hookProc = HookCallback;
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _hookProc,
            NativeMethods.GetModuleHandle(module.ModuleName!),
            0);

        if (_hookId == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            Trace.TraceWarning(
                "[GlobalHotkeyService] SetWindowsHookEx failed – Win32 error {0} (0x{0:X8})",
                error);
            return false;
        }

        Trace.TraceInformation(
            "[GlobalHotkeyService] Hook installed. Hotkey: modifiers=0x{0:X}, vk=0x{1:X}",
            (int)Hotkey.Modifiers, Hotkey.VirtualKey);
        return true;
    }

    /// <summary>
    /// Removes the keyboard hook. Safe to call multiple times.
    /// </summary>
    public void Unregister()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            Trace.TraceInformation("[GlobalHotkeyService] Hook removed.");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unregister();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            int msg = wParam.ToInt32();

            if (vkCode == Hotkey.VirtualKey)
            {
                if (msg is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN)
                {
                    // Only fire on the initial key-down, not auto-repeat
                    if (!_targetKeyDown && AreModifiersPressed())
                    {
                        _targetKeyDown = true;
                        HotkeyPressed?.Invoke(this, EventArgs.Empty);
                    }
                }
                else if (msg is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP)
                {
                    if (_targetKeyDown)
                    {
                        _targetKeyDown = false;
                        HotkeyReleased?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    /// <summary>
    /// Checks if the required modifier keys are currently pressed using GetAsyncKeyState.
    /// Special case: if the target key IS a Win key, skip the Win modifier check
    /// (the key itself being pressed would satisfy it, causing a false double-requirement).
    /// </summary>
    private bool AreModifiersPressed()
    {
        var required = Hotkey.Modifiers;
        bool isWinKey = Hotkey.VirtualKey is NativeMethods.VK_LWIN or NativeMethods.VK_RWIN;

        bool ctrlRequired = required.HasFlag(ModifierKeys.Control);
        bool shiftRequired = required.HasFlag(ModifierKeys.Shift);
        bool altRequired = required.HasFlag(ModifierKeys.Alt);
        bool winRequired = required.HasFlag(ModifierKeys.Win);

        bool ctrlDown = IsKeyDown(NativeMethods.VK_CONTROL);
        bool shiftDown = IsKeyDown(NativeMethods.VK_SHIFT);
        bool altDown = IsKeyDown(NativeMethods.VK_MENU);
        bool winDown = IsKeyDown(NativeMethods.VK_LWIN) || IsKeyDown(NativeMethods.VK_RWIN);

        return (ctrlRequired == ctrlDown) &&
               (shiftRequired == shiftDown) &&
               (altRequired == altDown) &&
               (isWinKey || (winRequired == winDown));
    }

    private static bool IsKeyDown(int vk) =>
        (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;
}
