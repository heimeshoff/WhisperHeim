namespace WhisperHeim.Services.Transcription;

/// <summary>
/// Result of a transcription operation.
/// </summary>
public sealed record TranscriptionResult(
    string Text,
    TimeSpan AudioDuration,
    TimeSpan TranscriptionDuration,
    double RealTimeFactor);

/// <summary>
/// Transcribes audio segments using an offline speech recognition model.
/// </summary>
public interface ITranscriptionService : IDisposable
{
    /// <summary>
    /// Whether the recognizer has been loaded and is ready for transcription.
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    /// Loads the speech recognition model. Must be called before transcribing.
    /// </summary>
    /// <exception cref="InvalidOperationException">If model files are missing.</exception>
    void LoadModel();

    /// <summary>
    /// Transcribes the given audio samples on a background thread.
    /// </summary>
    /// <param name="samples">Float32 PCM samples, 16 kHz mono.</param>
    /// <param name="sampleRate">Sample rate in Hz (expected 16000).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transcription result including text and timing metrics.</returns>
    Task<TranscriptionResult> TranscribeAsync(
        float[] samples,
        int sampleRate = 16000,
        CancellationToken cancellationToken = default);
}
