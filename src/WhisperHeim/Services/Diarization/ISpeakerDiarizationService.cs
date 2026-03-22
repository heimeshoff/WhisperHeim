namespace WhisperHeim.Services.Diarization;

/// <summary>
/// Identifies and separates speakers in audio recordings using offline speaker diarization.
/// </summary>
public interface ISpeakerDiarizationService : IDisposable
{
    /// <summary>
    /// Whether the diarization models have been loaded and the service is ready.
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    /// Loads the speaker segmentation and embedding models. Must be called before diarizing.
    /// </summary>
    /// <exception cref="InvalidOperationException">If model files are missing.</exception>
    void LoadModels();

    /// <summary>
    /// Performs speaker diarization on a single audio stream.
    /// Identifies speakers purely from acoustic features.
    /// </summary>
    /// <param name="samples">Float32 PCM samples, 16 kHz mono.</param>
    /// <param name="numSpeakers">
    /// Expected number of speakers. Use -1 to auto-detect (uses clustering threshold).
    /// </param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Diarization result with speaker segments.</returns>
    Task<DiarizationResult> DiarizeAsync(
        float[] samples,
        int numSpeakers = -1,
        IProgress<DiarizationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs speaker diarization with dual-stream attribution.
    /// Uses separate mic and loopback streams to attribute speakers:
    /// mic audio = local user, loopback audio = remote speakers.
    /// </summary>
    /// <param name="micSamples">Float32 PCM samples from the microphone, 16 kHz mono.</param>
    /// <param name="loopbackSamples">Float32 PCM samples from system audio loopback, 16 kHz mono.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Diarization result with source-attributed speaker segments.</returns>
    Task<IReadOnlyList<AttributedDiarizationSegment>> DiarizeDualStreamAsync(
        float[] micSamples,
        float[] loopbackSamples,
        IProgress<DiarizationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
