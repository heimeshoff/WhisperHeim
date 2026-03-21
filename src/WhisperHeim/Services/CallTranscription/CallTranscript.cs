using System.Text.Json.Serialization;

namespace WhisperHeim.Services.CallTranscription;

/// <summary>
/// A complete, structured transcript of a recorded call with speaker attribution
/// and timestamps for each segment.
/// </summary>
public sealed class CallTranscript
{
    /// <summary>Unique identifier for this transcript.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>UTC timestamp when the call recording started.</summary>
    [JsonPropertyName("recordingStartedUtc")]
    public required DateTimeOffset RecordingStartedUtc { get; init; }

    /// <summary>UTC timestamp when the call recording ended.</summary>
    [JsonPropertyName("recordingEndedUtc")]
    public required DateTimeOffset RecordingEndedUtc { get; init; }

    /// <summary>Total duration of the recorded call.</summary>
    [JsonPropertyName("duration")]
    public TimeSpan Duration => RecordingEndedUtc - RecordingStartedUtc;

    /// <summary>Ordered list of transcript segments with speaker labels and timestamps.</summary>
    [JsonPropertyName("segments")]
    public required IReadOnlyList<TranscriptSegment> Segments { get; init; }

    /// <summary>
    /// Path to the stored transcript JSON file, or null if not yet persisted.
    /// </summary>
    [JsonIgnore]
    public string? FilePath { get; set; }
}

/// <summary>
/// A single segment within a call transcript, representing one speaker's
/// continuous utterance with timing and attribution.
/// </summary>
public sealed class TranscriptSegment
{
    /// <summary>Display label for the speaker (e.g., "You", "Speaker 1").</summary>
    [JsonPropertyName("speaker")]
    public required string Speaker { get; init; }

    /// <summary>Start time of this segment relative to the recording start.</summary>
    [JsonPropertyName("startTime")]
    public required TimeSpan StartTime { get; init; }

    /// <summary>End time of this segment relative to the recording start.</summary>
    [JsonPropertyName("endTime")]
    public required TimeSpan EndTime { get; init; }

    /// <summary>Transcribed text for this segment.</summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    /// <summary>Whether this segment came from the microphone (local user) or loopback (remote).</summary>
    [JsonPropertyName("isLocalSpeaker")]
    public required bool IsLocalSpeaker { get; init; }
}
