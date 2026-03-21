using System.Runtime.InteropServices;

namespace WhisperHeim.Services.Input;

/// <summary>
/// P/Invoke declarations for Win32 SendInput and related structures.
/// </summary>
internal static class NativeInputMethods
{
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    internal const int INPUT_KEYBOARD = 1;

    internal const uint KEYEVENTF_KEYDOWN = 0x0000;
    internal const uint KEYEVENTF_KEYUP = 0x0002;
    internal const uint KEYEVENTF_UNICODE = 0x0004;

    // Virtual key codes
    internal const ushort VK_BACK = 0x08;
    internal const ushort VK_RETURN = 0x0D;

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public int type;
        public INPUTUNION u;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct INPUTUNION
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    /// <summary>
    /// Creates a pair of INPUT structs (key-down + key-up) for a Unicode character
    /// using KEYEVENTF_UNICODE so it works regardless of keyboard layout.
    /// </summary>
    internal static INPUT[] CreateUnicodeKeyPress(char c)
    {
        var down = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = KEYEVENTF_UNICODE,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        var up = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        return [down, up];
    }

    /// <summary>
    /// Creates a pair of INPUT structs for a virtual key press (e.g. VK_BACK).
    /// </summary>
    internal static INPUT[] CreateVirtualKeyPress(ushort virtualKeyCode)
    {
        var down = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = virtualKeyCode,
                    wScan = 0,
                    dwFlags = KEYEVENTF_KEYDOWN,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        var up = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = virtualKeyCode,
                    wScan = 0,
                    dwFlags = KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        return [down, up];
    }
}
