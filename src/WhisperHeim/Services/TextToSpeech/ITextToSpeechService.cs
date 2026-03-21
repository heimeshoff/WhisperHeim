namespace WhisperHeim.Services.TextToSpeech;

/// <summary>
/// Describes a voice that can be used for text-to-speech generation.
/// </summary>
public sealed record TtsVoice(
    string Id,
    string DisplayName,
    string ReferenceAudioPath,
    bool IsBuiltIn);

/// <summary>
/// Result of a text-to-speech generation operation.
/// </summary>
public sealed record TtsGenerationResult(
    float[] Samples,
    int SampleRate);

/// <summary>
/// Generates speech audio from text using Pocket TTS via sherpa-onnx.
/// </summary>
public interface ITextToSpeechService : IDisposable
{
    /// <summary>
    /// Whether the TTS model has been loaded and is ready for generation.
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    /// Loads the Pocket TTS model. Must be called before generating speech.
    /// </summary>
    /// <exception cref="InvalidOperationException">If model files are missing.</exception>
    void LoadModel();

    /// <summary>
    /// Returns all available voices (built-in + any custom voices from the voices directory).
    /// </summary>
    IReadOnlyList<TtsVoice> GetAvailableVoices();

    /// <summary>
    /// Generates speech audio from the given text using the specified voice.
    /// </summary>
    /// <param name="text">The text to synthesize.</param>
    /// <param name="voiceId">The voice ID to use (from <see cref="GetAvailableVoices"/>).</param>
    /// <param name="speed">Speech speed multiplier (1.0 = normal).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated audio samples at 24kHz mono.</returns>
    Task<TtsGenerationResult> GenerateAudioAsync(
        string text,
        string voiceId,
        float speed = 1.0f,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates speech audio in streaming mode, invoking the callback as chunks become available.
    /// This enables low-latency playback (first chunk within ~200ms).
    /// </summary>
    /// <param name="text">The text to synthesize.</param>
    /// <param name="voiceId">The voice ID to use.</param>
    /// <param name="onChunk">Callback invoked for each generated audio chunk (samples, progress 0-1).</param>
    /// <param name="speed">Speech speed multiplier (1.0 = normal).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task GenerateAudioStreamingAsync(
        string text,
        string voiceId,
        Action<float[], float> onChunk,
        float speed = 1.0f,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Plays the given text through the default audio output device.
    /// Combines streaming generation with NAudio playback for low-latency output.
    /// </summary>
    /// <param name="text">The text to speak.</param>
    /// <param name="voiceId">The voice ID to use.</param>
    /// <param name="speed">Speech speed multiplier (1.0 = normal).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SpeakAsync(
        string text,
        string voiceId,
        float speed = 1.0f,
        CancellationToken cancellationToken = default);
}
