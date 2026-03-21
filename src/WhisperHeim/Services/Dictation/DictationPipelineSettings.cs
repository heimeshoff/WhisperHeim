namespace WhisperHeim.Services.Dictation;

/// <summary>
/// Configuration settings for the streaming dictation pipeline.
/// </summary>
public sealed class DictationPipelineSettings
{
    /// <summary>
    /// Interval in milliseconds at which partial transcription results are generated
    /// during ongoing speech. This implements the "tumbling window" approach.
    /// Default: 1500ms (1.5 seconds).
    /// </summary>
    public int PartialResultIntervalMs { get; set; } = 1500;

    /// <summary>
    /// Minimum accumulated audio duration (in milliseconds) before a partial
    /// transcription is attempted. Prevents wasting ASR on tiny audio snippets.
    /// Default: 500ms.
    /// </summary>
    public int MinPartialAudioMs { get; set; } = 500;

    /// <summary>
    /// Audio sample rate in Hz. Must match the VAD and ASR expectations.
    /// Default: 16000.
    /// </summary>
    public int SampleRate { get; set; } = 16000;
}
