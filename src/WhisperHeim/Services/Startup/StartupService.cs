using Microsoft.Win32;

namespace WhisperHeim.Services.Startup;

/// <summary>
/// Manages the Windows auto-start registry entry for WhisperHeim.
/// Uses HKCU\Software\Microsoft\Windows\CurrentVersion\Run (per-user, no admin required).
/// </summary>
public sealed class StartupService
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "WhisperHeim";

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
    }

    /// <summary>
    /// Disables auto-start by removing the registry entry.
    /// </summary>
    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
        key?.DeleteValue(AppName, throwOnMissingValue: false);
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
}
