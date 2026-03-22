namespace WhisperHeim.Services.Diarization;

/// <summary>
/// A single speaker segment identified by diarization.
/// </summary>
public sealed record DiarizationSegment(
    /// <summary>Speaker identifier (0-based index).</summary>
    int SpeakerId,

    /// <summary>Start time of the segment.</summary>
    TimeSpan StartTime,

    /// <summary>End time of the segment.</summary>
    TimeSpan EndTime)
{
    /// <summary>Duration of this segment.</summary>
    public TimeSpan Duration => EndTime - StartTime;
}

/// <summary>
/// Result of a speaker diarization operation.
/// </summary>
public sealed record DiarizationResult(
    /// <summary>Ordered list of speaker segments.</summary>
    IReadOnlyList<DiarizationSegment> Segments,

    /// <summary>Total number of distinct speakers detected.</summary>
    int SpeakerCount,

    /// <summary>Duration of the processed audio.</summary>
    TimeSpan AudioDuration,

    /// <summary>Time taken for the diarization process.</summary>
    TimeSpan ProcessingDuration);

/// <summary>
/// Reports progress during a diarization operation.
/// </summary>
public sealed class DiarizationProgress
{
    /// <summary>Number of audio chunks processed so far.</summary>
    public int ProcessedChunks { get; init; }

    /// <summary>Total number of audio chunks to process.</summary>
    public int TotalChunks { get; init; }

    /// <summary>Progress percentage (0-100).</summary>
    public double Percent => TotalChunks > 0
        ? Math.Min(100.0, (double)ProcessedChunks / TotalChunks * 100.0)
        : 0.0;
}

/// <summary>
/// Indicates the source of a speaker when dual-stream attribution is used.
/// </summary>
public enum SpeakerSource
{
    /// <summary>Unknown source (single-stream diarization).</summary>
    Unknown,

    /// <summary>Speaker from microphone input (local user).</summary>
    Microphone,

    /// <summary>Speaker from loopback/system audio (remote participants).</summary>
    Loopback
}

/// <summary>
/// A diarization segment enriched with source attribution from dual-stream recording.
/// </summary>
public sealed record AttributedDiarizationSegment(
    /// <summary>Speaker identifier (0-based index).</summary>
    int SpeakerId,

    /// <summary>Start time of the segment.</summary>
    TimeSpan StartTime,

    /// <summary>End time of the segment.</summary>
    TimeSpan EndTime,

    /// <summary>Source attribution for this speaker.</summary>
    SpeakerSource Source)
{
    /// <summary>Duration of this segment.</summary>
    public TimeSpan Duration => EndTime - StartTime;
}
