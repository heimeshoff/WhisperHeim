namespace WhisperHeim.Services.CallTranscription;

/// <summary>
/// Reports progress of the call transcription pipeline.
/// </summary>
public sealed class TranscriptionPipelineProgress
{
    /// <summary>Current stage of the pipeline.</summary>
    public required PipelineStage Stage { get; init; }

    /// <summary>Progress percentage within the current stage (0-100).</summary>
    public required double StagePercent { get; init; }

    /// <summary>Overall progress percentage across the entire pipeline (0-100).</summary>
    public required double OverallPercent { get; init; }

    /// <summary>Human-readable description of the current activity.</summary>
    public required string Description { get; init; }
}

/// <summary>
/// Stages of the call transcription pipeline.
/// </summary>
public enum PipelineStage
{
    /// <summary>Loading audio files from disk.</summary>
    LoadingAudio,

    /// <summary>Running speaker diarization on the audio.</summary>
    Diarizing,

    /// <summary>Transcribing individual speaker segments.</summary>
    Transcribing,

    /// <summary>Assembling the final structured transcript.</summary>
    Assembling,

    /// <summary>Saving transcript to persistent storage.</summary>
    Saving,

    /// <summary>Pipeline completed successfully.</summary>
    Completed,
}
