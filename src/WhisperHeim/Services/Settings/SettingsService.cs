using System.IO;
using System.Text.Json;
using WhisperHeim.Models;

namespace WhisperHeim.Services.Settings;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON.
/// The settings file location is resolved by <see cref="DataPathService"/>.
/// Machine-local settings (window, overlay, audio device) live in the bootstrap config.
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly DataPathService _dataPathService;
    private AppSettings _current = new();

    public SettingsService(DataPathService dataPathService)
    {
        _dataPathService = dataPathService;
    }

    /// <summary>The current in-memory settings.</summary>
    public AppSettings Current => _current;

    /// <summary>The data path service for resolving paths and accessing machine-local settings.</summary>
    public DataPathService DataPathService => _dataPathService;

    /// <summary>
    /// Loads settings from disk. If the file does not exist, creates it with defaults.
    /// Also synchronizes machine-local settings from bootstrap config into the in-memory model.
    /// </summary>
    public void Load()
    {
        var settingsPath = _dataPathService.SettingsPath;
        try
        {
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
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

        // Synchronize machine-local settings from bootstrap config into the in-memory model
        // so that existing code reading Current.Window/Overlay/Dictation.AudioDevice still works.
        SyncFromBootstrap();
    }

    /// <summary>
    /// Persists the current settings to disk.
    /// Machine-local settings are also persisted to the bootstrap config.
    /// </summary>
    public void Save()
    {
        // Before saving, push machine-local settings back to bootstrap config
        SyncToBootstrap();

        var settingsPath = _dataPathService.SettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        var json = JsonSerializer.Serialize(_current, JsonOptions);
        File.WriteAllText(settingsPath, json);
    }

    /// <summary>
    /// Copies machine-local settings from bootstrap config into the in-memory AppSettings.
    /// This allows existing code to read window/overlay/device settings from AppSettings.
    /// </summary>
    private void SyncFromBootstrap()
    {
        var bootstrap = _dataPathService.Bootstrap;
        _current.Window = bootstrap.Window;
        _current.Overlay = bootstrap.Overlay;
        _current.Dictation.AudioDevice = bootstrap.AudioDevice;
        _current.Tts.PlaybackDeviceId = bootstrap.TtsPlaybackDeviceId;
    }

    /// <summary>
    /// Pushes machine-local settings from AppSettings back into the bootstrap config.
    /// </summary>
    private void SyncToBootstrap()
    {
        var bootstrap = _dataPathService.Bootstrap;
        bootstrap.Window = _current.Window;
        bootstrap.Overlay = _current.Overlay;
        bootstrap.AudioDevice = _current.Dictation.AudioDevice;
        bootstrap.TtsPlaybackDeviceId = _current.Tts.PlaybackDeviceId;
        _dataPathService.Save();
    }
}
