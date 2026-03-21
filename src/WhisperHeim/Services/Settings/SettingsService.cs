using System.IO;
using System.Text.Json;
using WhisperHeim.Models;

namespace WhisperHeim.Services.Settings;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON in %APPDATA%/WhisperHeim/settings.json.
/// Creates the file with defaults on first run.
/// </summary>
public sealed class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WhisperHeim");

    private static readonly string SettingsPath =
        Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private AppSettings _current = new();

    /// <summary>The current in-memory settings.</summary>
    public AppSettings Current => _current;

    /// <summary>
    /// Loads settings from disk. If the file does not exist, creates it with defaults.
    /// </summary>
    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                _current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            else
            {
                _current = new AppSettings();
                Save(); // create the file with defaults
            }
        }
        catch
        {
            // If the file is corrupt, reset to defaults
            _current = new AppSettings();
            Save();
        }
    }

    /// <summary>
    /// Persists the current settings to disk.
    /// </summary>
    public void Save()
    {
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(_current, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
