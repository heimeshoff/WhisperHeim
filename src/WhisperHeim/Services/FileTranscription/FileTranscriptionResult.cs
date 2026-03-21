namespace WhisperHeim.Services.FileTranscription;

/// <summary>
/// Result of a file transcription operation.
/// </summary>
public sealed record FileTranscriptionResult(
    /// <summary>Full transcribed text.</summary>
    string Text,
    /// <summary>Duration of the source audio file.</summary>
    TimeSpan AudioDuration,
    /// <summary>Wall-clock time spent transcribing.</summary>
    TimeSpan TranscriptionDuration,
    /// <summary>Ratio of transcription time to audio duration (lower is faster).</summary>
    double RealTimeFactor,
    /// <summary>Number of chunks the audio was split into.</summary>
    int ChunkCount,
    /// <summary>Source file path that was transcribed.</summary>
    string SourceFilePath);
