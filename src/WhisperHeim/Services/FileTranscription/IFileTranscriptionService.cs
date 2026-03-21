namespace WhisperHeim.Services.FileTranscription;

/// <summary>
/// Transcribes audio files (OGG, MP3, M4A, WAV) by decoding to PCM
/// and feeding to the underlying transcription engine.
/// </summary>
public interface IFileTranscriptionService
{
    /// <summary>
    /// Supported audio file extensions (lowercase, with leading dot).
    /// </summary>
    IReadOnlySet<string> SupportedExtensions { get; }

    /// <summary>
    /// Returns true if the given file extension is supported.
    /// </summary>
    bool IsSupported(string filePath);

    /// <summary>
    /// Transcribes the audio file at the given path.
    /// Long files are automatically chunked at silence boundaries.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <param name="progress">Optional progress callback (0.0 to 1.0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transcription result with text and metadata.</returns>
    /// <exception cref="FileNotFoundException">If the file does not exist.</exception>
    /// <exception cref="NotSupportedException">If the file format is not supported.</exception>
    /// <exception cref="InvalidOperationException">If the file is corrupt or cannot be decoded.</exception>
    Task<FileTranscriptionResult> TranscribeFileAsync(
        string filePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
