using System.Text.Json.Serialization;

namespace WhisperHeim.Models;

/// <summary>
/// Application settings persisted as JSON in %APPDATA%/WhisperHeim/settings.json.
/// </summary>
public sealed class AppSettings
{
    /// <summary>General settings.</summary>
    [JsonPropertyName("general")]
    public GeneralSettings General { get; set; } = new();

    /// <summary>Dictation / transcription settings.</summary>
    [JsonPropertyName("dictation")]
    public DictationSettings Dictation { get; set; } = new();

    /// <summary>Template settings.</summary>
    [JsonPropertyName("templates")]
    public TemplateSettings Templates { get; set; } = new();

    /// <summary>Overlay indicator settings.</summary>
    [JsonPropertyName("overlay")]
    public OverlaySettings Overlay { get; set; } = new();
}

public sealed class GeneralSettings
{
    /// <summary>Launch WhisperHeim when Windows starts.</summary>
    [JsonPropertyName("launchAtStartup")]
    public bool LaunchAtStartup { get; set; }

    /// <summary>Start minimized to the system tray.</summary>
    [JsonPropertyName("startMinimized")]
    public bool StartMinimized { get; set; } = true;

    /// <summary>Application theme: "Dark", "Light", or "System".</summary>
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "Dark";
}

public sealed class DictationSettings
{
    /// <summary>Preferred audio input device name (null = system default).</summary>
    [JsonPropertyName("audioDevice")]
    public string? AudioDevice { get; set; }

    /// <summary>Whisper language code (e.g. "en", "de"). Null = auto-detect.</summary>
    [JsonPropertyName("language")]
    public string? Language { get; set; }
}

public sealed class TemplateSettings
{
    /// <summary>User-defined text templates (future use).</summary>
    [JsonPropertyName("items")]
    public List<string> Items { get; set; } = [];
}

/// <summary>
/// Settings for the dictation overlay indicator window.
/// </summary>
public sealed class OverlaySettings
{
    /// <summary>Whether the overlay indicator is enabled.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Overlay opacity (0.0 to 1.0).</summary>
    [JsonPropertyName("opacity")]
    public double Opacity { get; set; } = 0.85;

    /// <summary>Overlay size in pixels.</summary>
    [JsonPropertyName("size")]
    public int Size { get; set; } = 48;

    /// <summary>
    /// Overlay position: "BottomCenter", "BottomLeft", "BottomRight",
    /// "TopCenter", "TopLeft", "TopRight".
    /// </summary>
    [JsonPropertyName("position")]
    public string Position { get; set; } = "BottomCenter";
}
