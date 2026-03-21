namespace WhisperHeim.Services.Audio;

/// <summary>
/// Service interface for capturing microphone audio.
/// </summary>
public interface IAudioCaptureService : IDisposable
{
    /// <summary>
    /// Raised when a chunk of float32 normalized audio samples is available.
    /// </summary>
    event EventHandler<AudioDataEventArgs>? AudioDataAvailable;

    /// <summary>
    /// Raised when capture has started.
    /// </summary>
    event EventHandler? CaptureStarted;

    /// <summary>
    /// Raised when capture has stopped (user-initiated or device disconnection).
    /// </summary>
    event EventHandler<CaptureStoppedEventArgs>? CaptureStopped;

    /// <summary>
    /// Whether capture is currently active.
    /// </summary>
    bool IsCapturing { get; }

    /// <summary>
    /// Enumerates available audio input devices.
    /// </summary>
    IReadOnlyList<AudioDeviceInfo> GetAvailableDevices();

    /// <summary>
    /// Starts capturing audio from the specified device (or default if -1).
    /// </summary>
    void StartCapture(int deviceIndex = -1);

    /// <summary>
    /// Stops capturing audio.
    /// </summary>
    void StopCapture();
}

/// <summary>
/// Event args carrying float32 normalized audio samples.
/// </summary>
public sealed class AudioDataEventArgs : EventArgs
{
    public AudioDataEventArgs(float[] samples)
    {
        Samples = samples;
    }

    /// <summary>
    /// Float32 normalized audio samples in [-1.0, 1.0] range, 16kHz mono.
    /// </summary>
    public float[] Samples { get; }
}

/// <summary>
/// Event args for capture stopped, including possible error info.
/// </summary>
public sealed class CaptureStoppedEventArgs : EventArgs
{
    public CaptureStoppedEventArgs(bool wasDeviceDisconnected, Exception? exception = null)
    {
        WasDeviceDisconnected = wasDeviceDisconnected;
        Exception = exception;
    }

    public bool WasDeviceDisconnected { get; }
    public Exception? Exception { get; }
}
