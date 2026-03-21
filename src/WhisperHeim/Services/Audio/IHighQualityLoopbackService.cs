namespace WhisperHeim.Services.Audio;

/// <summary>
/// Service interface for capturing system audio at native quality (not downsampled)
/// for use as voice cloning reference audio.
/// </summary>
public interface IHighQualityLoopbackService : IDisposable
{
    /// <summary>
    /// Raised when a chunk of float32 audio samples is available (native format).
    /// </summary>
    event EventHandler<HighQualityAudioEventArgs>? AudioDataAvailable;

    /// <summary>
    /// Raised when capture has started.
    /// </summary>
    event EventHandler? CaptureStarted;

    /// <summary>
    /// Raised when capture has stopped.
    /// </summary>
    event EventHandler<CaptureStoppedEventArgs>? CaptureStopped;

    /// <summary>
    /// Whether capture is currently active.
    /// </summary>
    bool IsCapturing { get; }

    /// <summary>
    /// Current recording duration.
    /// </summary>
    TimeSpan Duration { get; }

    /// <summary>
    /// Enumerates available audio output (render) devices for loopback capture.
    /// </summary>
    IReadOnlyList<AudioDeviceInfo> GetAvailableDevices();

    /// <summary>
    /// Starts capturing system audio at native quality, writing to a temporary WAV file.
    /// </summary>
    void StartCapture(int deviceIndex = -1);

    /// <summary>
    /// Stops capturing and finalizes the WAV file.
    /// </summary>
    void StopCapture();

    /// <summary>
    /// Gets the path to the temporary WAV file from the last recording.
    /// </summary>
    string? TempWavFilePath { get; }

    /// <summary>
    /// Saves the captured audio to the custom voices directory with the given name.
    /// Returns the full path to the saved WAV file.
    /// </summary>
    string SaveAsVoice(string voiceName);
}

/// <summary>
/// Event args carrying high-quality float32 audio samples with RMS level.
/// </summary>
public sealed class HighQualityAudioEventArgs : EventArgs
{
    public HighQualityAudioEventArgs(float[] samples, float rmsLevel)
    {
        Samples = samples;
        RmsLevel = rmsLevel;
    }

    /// <summary>Float32 audio samples at native sample rate.</summary>
    public float[] Samples { get; }

    /// <summary>RMS level of this chunk (0.0 to 1.0).</summary>
    public float RmsLevel { get; }
}
