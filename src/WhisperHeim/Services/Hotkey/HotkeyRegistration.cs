using System.Globalization;

namespace WhisperHeim.Services.Hotkey;

/// <summary>
/// Represents a hotkey as a modifier + virtual key combination.
/// </summary>
public sealed record HotkeyRegistration(ModifierKeys Modifiers, int VirtualKey)
{
    /// <summary>
    /// Default dictation hotkey: Ctrl + Left Windows key.
    /// Uses low-level keyboard hook (not RegisterHotKey) to capture Win key combos.
    /// </summary>
    public static HotkeyRegistration Default { get; } = new(
        ModifierKeys.Control,
        VirtualKey: NativeMethods.VK_LWIN
    );

    /// <summary>
    /// Converts the hotkey to a human-readable string like "Ctrl+Shift+R".
    /// </summary>
    public string ToDisplayString()
    {
        var parts = new List<string>();

        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Win)) parts.Add("Win");

        // Convert virtual key code to a readable name
        parts.Add(VirtualKeyToString(VirtualKey));

        return string.Join("+", parts);
    }

    /// <summary>
    /// Parses a hotkey string like "Ctrl+Shift+R" into a <see cref="HotkeyRegistration"/>.
    /// Returns null if parsing fails.
    /// </summary>
    public static HotkeyRegistration? TryParse(string? hotkeyString)
    {
        if (string.IsNullOrWhiteSpace(hotkeyString))
            return null;

        var parts = hotkeyString.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return null;

        var modifiers = ModifierKeys.None;
        int virtualKey = 0;

        foreach (var part in parts)
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= ModifierKeys.Control;
                    break;
                case "ALT":
                    modifiers |= ModifierKeys.Alt;
                    break;
                case "SHIFT":
                    modifiers |= ModifierKeys.Shift;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= ModifierKeys.Win;
                    break;
                default:
                    virtualKey = StringToVirtualKey(part);
                    if (virtualKey == 0)
                        return null; // Unknown key
                    break;
            }
        }

        if (virtualKey == 0)
            return null; // No primary key found

        return new HotkeyRegistration(modifiers, virtualKey);
    }

    /// <summary>
    /// Converts a virtual key code to a display string.
    /// </summary>
    private static string VirtualKeyToString(int vk) => vk switch
    {
        >= 0x41 and <= 0x5A => ((char)vk).ToString(), // A-Z
        >= 0x30 and <= 0x39 => ((char)vk).ToString(), // 0-9
        >= 0x70 and <= 0x87 => $"F{vk - 0x6F}",       // F1-F24
        NativeMethods.VK_SPACE => "Space",
        NativeMethods.VK_LWIN => "LWin",
        NativeMethods.VK_RWIN => "RWin",
        _ => $"0x{vk:X2}"
    };

    /// <summary>
    /// Converts a key name string to a virtual key code.
    /// </summary>
    private static int StringToVirtualKey(string key)
    {
        var upper = key.ToUpperInvariant();

        // Single letter A-Z
        if (upper.Length == 1 && upper[0] >= 'A' && upper[0] <= 'Z')
            return upper[0];

        // Single digit 0-9
        if (upper.Length == 1 && upper[0] >= '0' && upper[0] <= '9')
            return upper[0];

        // Function keys F1-F24
        if (upper.StartsWith('F') && int.TryParse(upper[1..], NumberStyles.None, CultureInfo.InvariantCulture, out int fNum) && fNum >= 1 && fNum <= 24)
            return 0x6F + fNum;

        return upper switch
        {
            "SPACE" => NativeMethods.VK_SPACE,
            "LWIN" => NativeMethods.VK_LWIN,
            "RWIN" => NativeMethods.VK_RWIN,
            _ => upper.StartsWith("0X", StringComparison.OrdinalIgnoreCase)
                ? int.TryParse(upper[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex) ? hex : 0
                : 0
        };
    }
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
