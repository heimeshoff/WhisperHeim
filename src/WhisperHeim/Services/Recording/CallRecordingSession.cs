namespace WhisperHeim.Services.Recording;

/// <summary>
/// Immutable snapshot of a call recording session, including file paths
/// and timing information for both the microphone and system audio streams.
/// </summary>
public sealed class CallRecordingSession
{
    public CallRecordingSession(
        string micWavFilePath,
        string systemWavFilePath,
        DateTimeOffset startTimestamp)
    {
        MicWavFilePath = micWavFilePath;
        SystemWavFilePath = systemWavFilePath;
        StartTimestamp = startTimestamp;
    }

    /// <summary>
    /// Path to the WAV file containing the microphone (local speaker) audio.
    /// </summary>
    public string MicWavFilePath { get; }

    /// <summary>
    /// Path to the WAV file containing the system (remote speaker) audio.
    /// </summary>
    public string SystemWavFilePath { get; }

    /// <summary>
    /// UTC timestamp when the recording session started. Both streams are
    /// aligned to this common reference point.
    /// </summary>
    public DateTimeOffset StartTimestamp { get; }

    /// <summary>
    /// UTC timestamp when the recording session ended, or null if still recording.
    /// </summary>
    public DateTimeOffset? EndTimestamp { get; internal set; }

    /// <summary>
    /// Duration of the recording. Returns the elapsed time since start if still recording.
    /// </summary>
    public TimeSpan Duration => (EndTimestamp ?? DateTimeOffset.UtcNow) - StartTimestamp;
}
