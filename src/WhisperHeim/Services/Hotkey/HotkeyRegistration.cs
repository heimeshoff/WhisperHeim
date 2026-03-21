namespace WhisperHeim.Services.Hotkey;

/// <summary>
/// Represents a hotkey as a modifier + virtual key combination.
/// </summary>
public sealed record HotkeyRegistration(ModifierKeys Modifiers, int VirtualKey)
{
    /// <summary>
    /// Default hotkey: Ctrl + Left Windows key.
    /// </summary>
    public static HotkeyRegistration Default { get; } = new(
        ModifierKeys.Control | ModifierKeys.Win,
        VirtualKey: NativeMethods.VK_LWIN
    );
}

[Flags]
public enum ModifierKeys
{
    None = 0x0000,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
}
