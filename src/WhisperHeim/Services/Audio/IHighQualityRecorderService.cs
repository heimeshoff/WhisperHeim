namespace WhisperHeim.Services.Audio;

/// <summary>
/// Service for recording high-quality microphone audio (44.1/48kHz)
/// suitable for TTS voice cloning reference samples.
/// </summary>
public interface IHighQualityRecorderService : IDisposable
{
    /// <summary>
    /// Whether recording is currently active.
    /// </summary>
    bool IsRecording { get; }

    /// <summary>
    /// Current recording duration.
    /// </summary>
    TimeSpan Duration { get; }

    /// <summary>
    /// Raised when the audio level (RMS) changes during recording.
    /// Value is in [0.0, 1.0] range.
    /// </summary>
    event EventHandler<float>? LevelChanged;

    /// <summary>
    /// Raised when the recording duration updates (approximately every 100ms).
    /// </summary>
    event EventHandler<TimeSpan>? DurationChanged;

    /// <summary>
    /// Raised when recording stops (user-initiated or error).
    /// </summary>
    event EventHandler<RecordingStoppedEventArgs>? RecordingStopped;

    /// <summary>
    /// Enumerates available audio input devices (reuses NAudio WaveIn enumeration).
    /// </summary>
    IReadOnlyList<AudioDeviceInfo> GetAvailableDevices();

    /// <summary>
    /// Starts recording from the specified device at high quality (44.1kHz or 48kHz mono).
    /// </summary>
    /// <param name="deviceIndex">Device index, or -1 for system default.</param>
    void StartRecording(int deviceIndex = -1);

    /// <summary>
    /// Stops recording and returns the path to the temporary WAV file.
    /// </summary>
    /// <returns>Path to the recorded WAV file, or null if no audio was recorded.</returns>
    string? StopRecording();

    /// <summary>
    /// Saves the last recording to the specified path.
    /// </summary>
    /// <param name="destinationPath">Full path for the output .wav file.</param>
    /// <returns>True if saved successfully.</returns>
    bool SaveRecording(string destinationPath);
}

/// <summary>
/// Event args for when high-quality recording stops.
/// </summary>
public sealed class RecordingStoppedEventArgs : EventArgs
{
    public RecordingStoppedEventArgs(bool success, string? filePath, Exception? exception = null)
    {
        Success = success;
        FilePath = filePath;
        Exception = exception;
    }

    public bool Success { get; }
    public string? FilePath { get; }
    public Exception? Exception { get; }
}
