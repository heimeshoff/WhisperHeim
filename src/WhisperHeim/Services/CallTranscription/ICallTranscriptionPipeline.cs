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
    /// <param name="progress">Optional progress reporter for UI updates.</param>
    /// <param name="cancellationToken">Cancellation token to abort processing.</param>
    /// <returns>The completed, persisted call transcript.</returns>
    /// <exception cref="InvalidOperationException">If required models are not loaded.</exception>
    /// <exception cref="OperationCanceledException">If processing was cancelled.</exception>
    Task<CallTranscript> ProcessAsync(
        CallRecordingSession session,
        IProgress<TranscriptionPipelineProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
