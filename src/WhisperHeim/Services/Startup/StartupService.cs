using Microsoft.Win32;

namespace WhisperHeim.Services.Startup;

/// <summary>
/// Manages the Windows auto-start registry entry for WhisperHeim.
/// Uses HKCU\Software\Microsoft\Windows\CurrentVersion\Run (per-user, no admin required).
/// Also manages the StartupApproved\Run entry required by Windows 11 to honour the Run key.
/// </summary>
public sealed class StartupService
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string AppName = "WhisperHeim";

    // 12-byte REG_BINARY: first byte 02 = enabled, 03 = disabled.
    // Remaining bytes are typically a FILETIME timestamp (zeros is valid).
    private static readonly byte[] EnabledBytes = [0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
    private static readonly byte[] DisabledBytes = [0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

    /// <summary>
    /// Returns the command line that should be written to the registry.
    /// Uses the current exe path with --minimized flag.
    /// </summary>
    private static string GetStartupCommand()
    {
        var exePath = Environment.ProcessPath
            ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Cannot determine executable path.");

        return $"\"{exePath}\" --minimized";
    }

    /// <summary>
    /// Enables auto-start by creating/updating the registry entry.
    /// </summary>
    public void Enable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
        key?.SetValue(AppName, GetStartupCommand());

        SetStartupApproved(enabled: true);
    }

    /// <summary>
    /// Disables auto-start by removing the registry entry.
    /// </summary>
    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
        key?.DeleteValue(AppName, throwOnMissingValue: false);

        SetStartupApproved(enabled: false);
    }

    /// <summary>
    /// Returns true if an auto-start registry entry exists for this app.
    /// </summary>
    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: false);
        return key?.GetValue(AppName) is not null;
    }

    /// <summary>
    /// If auto-start is enabled, updates the registry entry to point to the current exe path.
    /// This handles the case where the exe path changes (e.g., after an update).
    /// </summary>
    public void RefreshIfEnabled()
    {
        if (IsEnabled())
        {
            Enable();
        }
    }

    /// <summary>
    /// Synchronizes the registry with the desired state.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        if (enabled)
            Enable();
        else
            Disable();
    }

    /// <summary>
    /// Writes the StartupApproved\Run entry that Windows 11 checks to decide
    /// whether a Run-key entry is allowed to launch at logon.
    /// </summary>
    private static void SetStartupApproved(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(StartupApprovedKeyPath);
        if (enabled)
            key.SetValue(AppName, EnabledBytes, RegistryValueKind.Binary);
        else
            key.DeleteValue(AppName, throwOnMissingValue: false);
    }
}
