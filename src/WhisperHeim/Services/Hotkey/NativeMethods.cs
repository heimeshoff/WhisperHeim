using System.Runtime.InteropServices;

namespace WhisperHeim.Services.Hotkey;

/// <summary>
/// P/Invoke declarations for Win32 hotkey registration.
/// </summary>
internal static partial class NativeMethods
{
    public const int WM_HOTKEY = 0x0312;
    public const int VK_LWIN = 0x5B;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(IntPtr hWnd, int id);
}
