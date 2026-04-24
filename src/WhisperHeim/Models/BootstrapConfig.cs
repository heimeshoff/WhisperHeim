using System.Text.Json.Serialization;

namespace WhisperHeim.Models;

/// <summary>
/// Bootstrap configuration stored in %APPDATA%\WhisperHeim\bootstrap.json.
/// Contains the pointer to the synced data path and machine-local settings
/// that should not be synced across devices (window position, overlay, audio device).
/// </summary>
public sealed class BootstrapConfig
{
    /// <summary>
    /// Path to the synced data folder. Null or empty means use the default
    /// (%APPDATA%\WhisperHeim\), co-located with the bootstrap config.
    /// </summary>
    [JsonPropertyName("dataPath")]
    public string? DataPath { get; set; }

    /// <summary>Machine-local window size and position persistence.</summary>
    [JsonPropertyName("window")]
    public WindowSettings Window { get; set; } = new();

    /// <summary>Machine-local overlay indicator settings.</summary>
    [JsonPropertyName("overlay")]
    public OverlaySettings Overlay { get; set; } = new();

    /// <summary>Machine-local audio device selection for dictation.</summary>
    [JsonPropertyName("audioDevice")]
    public string? AudioDevice { get; set; }

    /// <summary>Machine-local TTS playback device.</summary>
    [JsonPropertyName("ttsPlaybackDeviceId")]
    public string? TtsPlaybackDeviceId { get; set; }

    /// <summary>
    /// Machine-local Ollama API endpoint URL. Different machines may run
    /// different Ollama servers, so this setting is not synced.
    /// </summary>
    [JsonPropertyName("ollamaEndpoint")]
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Machine-local Ollama model name (e.g. "qwen2.5:14b"). Different machines
    /// may have different models pulled, so this setting is not synced.
    /// </summary>
    [JsonPropertyName("ollamaModel")]
    public string? OllamaModel { get; set; }
}
