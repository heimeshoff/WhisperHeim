namespace WhisperHeim.Services.Audio;

/// <summary>
/// Configuration settings for the Silero Voice Activity Detector.
/// </summary>
public sealed class VadSettings
{
    /// <summary>
    /// Probability threshold above which a frame is considered speech.
    /// Range [0.0, 1.0]. Default: 0.5.
    /// </summary>
    public float SpeechThreshold { get; set; } = 0.5f;

    /// <summary>
    /// Probability threshold below which speech is considered to have ended.
    /// Should be less than <see cref="SpeechThreshold"/> to provide hysteresis.
    /// Default: 0.35.
    /// </summary>
    public float SilenceThreshold { get; set; } = 0.35f;

    /// <summary>
    /// Minimum duration of speech in milliseconds before a SpeechStarted event is fired.
    /// Prevents very short noise bursts from triggering speech. Default: 250ms.
    /// </summary>
    public int MinSpeechDurationMs { get; set; } = 250;

    /// <summary>
    /// Minimum duration of silence in milliseconds after speech before SpeechEnded is fired.
    /// Prevents brief pauses from splitting a single utterance. Default: 500ms.
    /// </summary>
    public int MinSilenceDurationMs { get; set; } = 500;

    /// <summary>
    /// Number of audio samples per VAD inference chunk.
    /// Silero VAD via sherpa-onnx uses 512 at 16kHz (~32ms per frame). Default: 512.
    /// </summary>
    public int ChunkSamples { get; set; } = 512;

    /// <summary>
    /// Audio sample rate in Hz. Must be 16000 for Silero VAD. Default: 16000.
    /// </summary>
    public int SampleRate { get; set; } = 16000;

    /// <summary>
    /// Amount of audio (in milliseconds) to prepend before the detected speech start,
    /// to avoid clipping the beginning of words. Default: 100ms.
    /// </summary>
    public int PreSpeechPadMs { get; set; } = 100;
}
