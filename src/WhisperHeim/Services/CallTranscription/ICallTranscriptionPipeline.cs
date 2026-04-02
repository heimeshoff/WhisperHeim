using WhisperHeim.Services.Recording;

namespace WhisperHeim.Services.CallTranscription;

/// <summary>
/// Orchestrates the end-to-end call transcription pipeline:
/// diarize recorded audio, transcribe each speaker segment,
/// and assemble a structured transcript with timestamps and speaker labels.
/// </summary>
public interface ICallTranscriptionPipeline
{
    /// <summary>
    /// Processes a completed call recording session through the full pipeline:
    /// load audio -> diarize -> transcribe segments -> assemble transcript -> save.
    /// </summary>
    /// <param name="session">The completed call recording session with audio file paths.</param>
    /// <param name="remoteSpeakerNames">
    /// Optional list of remote speaker names. When non-empty, the list length is used
    /// as numSpeakers for loopback diarization instead of auto-detect.
    /// </param>
    /// <param name="localSpeakerName">
    /// Display name for the local (mic) speaker. Falls back to "You" if null or empty.
    /// </param>
    /// <param name="progress">Optional progress reporter for UI updates.</param>
    /// <param name="cancellationToken">Cancellation token to abort processing.</param>
    /// <returns>The completed, persisted call transcript.</returns>
    /// <exception cref="InvalidOperationException">If required models are not loaded.</exception>
    /// <exception cref="OperationCanceledException">If processing was cancelled.</exception>
    Task<CallTranscript> ProcessAsync(
        CallRecordingSession session,
        IReadOnlyList<string>? remoteSpeakerNames = null,
        string? localSpeakerName = null,
        string? transcriptName = null,
        IProgress<TranscriptionPipelineProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
