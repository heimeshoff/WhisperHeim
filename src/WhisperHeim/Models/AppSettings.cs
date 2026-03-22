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

    /// <summary>Text-to-speech settings.</summary>
    [JsonPropertyName("tts")]
    public TtsSettings Tts { get; set; } = new();

    /// <summary>Window size and position persistence.</summary>
    [JsonPropertyName("window")]
    public WindowSettings Window { get; set; } = new();
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
    public string Theme { get; set; } = "Light";
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
    /// <summary>User-defined text templates as name/text pairs.</summary>
    [JsonPropertyName("items")]
    public List<TemplateItem> Items { get; set; } = [];
}

/// <summary>
/// A named text template that can be triggered by voice command.
/// </summary>
public sealed class TemplateItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
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

/// <summary>
/// Persisted window size, position, and state.
/// </summary>
public sealed class WindowSettings
{
    /// <summary>Window left position (null = not yet saved).</summary>
    [JsonPropertyName("left")]
    public double? Left { get; set; }

    /// <summary>Window top position (null = not yet saved).</summary>
    [JsonPropertyName("top")]
    public double? Top { get; set; }

    /// <summary>Window width (null = use default).</summary>
    [JsonPropertyName("width")]
    public double? Width { get; set; }

    /// <summary>Window height (null = use default).</summary>
    [JsonPropertyName("height")]
    public double? Height { get; set; }

    /// <summary>Whether the window was maximized.</summary>
    [JsonPropertyName("isMaximized")]
    public bool IsMaximized { get; set; }

    /// <summary>Whether the sidebar was collapsed to icons-only mode.</summary>
    [JsonPropertyName("sidebarCollapsed")]
    public bool SidebarCollapsed { get; set; }
}

/// <summary>
/// Settings for text-to-speech (TTS) features.
/// </summary>
public sealed class TtsSettings
{
    /// <summary>
    /// Default voice ID for TTS. Null means use the first available voice.
    /// Value corresponds to <see cref="WhisperHeim.Services.TextToSpeech.TtsVoice.Id"/>.
    /// </summary>
    [JsonPropertyName("defaultVoiceId")]
    public string? DefaultVoiceId { get; set; }

    /// <summary>
    /// Read-aloud hotkey combination as a string like "Ctrl+Win+Ä".
    /// Null means use the default (Ctrl+Win+Ä).
    /// </summary>
    [JsonPropertyName("readAloudHotkey")]
    public string? ReadAloudHotkey { get; set; }

    /// <summary>
    /// Playback device ID (NAudio device number as string). Null means system default (-1).
    /// </summary>
    [JsonPropertyName("playbackDeviceId")]
    public string? PlaybackDeviceId { get; set; }
}
