namespace WhisperHeim.Services.Audio;

/// <summary>
/// Detects voice activity (speech boundaries) in a stream of audio samples.
/// </summary>
public interface IVoiceActivityDetector : IDisposable
{
    /// <summary>
    /// Raised when speech is detected (after meeting the minimum speech duration).
    /// </summary>
    event EventHandler? SpeechStarted;

    /// <summary>
    /// Raised when speech has ended (after meeting the minimum silence duration).
    /// Contains the accumulated speech audio samples.
    /// </summary>
    event EventHandler<SpeechEndedEventArgs>? SpeechEnded;

    /// <summary>
    /// Whether speech is currently being detected.
    /// </summary>
    bool IsSpeechDetected { get; }

    /// <summary>
    /// Processes a chunk of float32 normalized audio samples through the VAD.
    /// Samples should be 16kHz mono float32 in [-1.0, 1.0].
    /// </summary>
    void ProcessAudio(float[] samples);

    /// <summary>
    /// Resets internal VAD state (ONNX hidden states and accumulators).
    /// </summary>
    void Reset();
}

/// <summary>
/// Event args carrying the accumulated speech audio segment.
/// </summary>
public sealed class SpeechEndedEventArgs : EventArgs
{
    public SpeechEndedEventArgs(float[] speechAudio)
    {
        SpeechAudio = speechAudio;
    }

    /// <summary>
    /// Float32 normalized audio samples of the detected speech segment, 16kHz mono.
    /// </summary>
    public float[] SpeechAudio { get; }
}
