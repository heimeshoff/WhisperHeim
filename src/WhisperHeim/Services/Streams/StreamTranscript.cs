using System.Text.Json.Serialization;

namespace WhisperHeim.Services.Streams;

/// <summary>
/// A transcription of a video from a stream URL (YouTube, Instagram, etc.).
/// </summary>
public sealed class StreamTranscript
{
    /// <summary>Unique identifier for this transcript.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Title of the video (from metadata).</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    /// <summary>Source URL that was transcribed.</summary>
    [JsonPropertyName("sourceUrl")]
    public required string SourceUrl { get; init; }

    /// <summary>Plain-text transcript content.</summary>
    [JsonPropertyName("transcriptText")]
    public string TranscriptText { get; set; } = "";

    /// <summary>Duration of the original video.</summary>
    [JsonPropertyName("duration")]
    public TimeSpan Duration { get; set; }

    /// <summary>UTC timestamp when the transcription was completed.</summary>
    [JsonPropertyName("dateTranscribedUtc")]
    public required DateTimeOffset DateTranscribedUtc { get; init; }

    /// <summary>Method used to obtain the transcript (captions vs local ASR).</summary>
    [JsonPropertyName("transcriptionMethod")]
    public string TranscriptionMethod { get; set; } = "";

    /// <summary>
    /// Path to the stored JSON file on disk, or null if not yet persisted.
    /// Not serialized.
    /// </summary>
    [JsonIgnore]
    public string? FilePath { get; set; }
}
