namespace WhisperHeim.Services.Dictation;

/// <summary>
/// Event args for dictation text results (both partial and final).
/// </summary>
public sealed class DictationResultEventArgs : EventArgs
{
    public DictationResultEventArgs(string text, bool isFinal)
    {
        Text = text;
        IsFinal = isFinal;
    }

    /// <summary>
    /// The transcribed text. For partial results, this is the new (diff) text
    /// since the last partial result. For final results, this is the complete
    /// segment text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Whether this is a final result (speech segment ended) or a partial update.
    /// </summary>
    public bool IsFinal { get; }
}

/// <summary>
/// Orchestrates the streaming dictation pipeline: AudioCapture -> VAD -> ASR -> Text.
/// Produces live partial text updates during speech and finalized text on speech end.
/// </summary>
public interface IDictationPipeline : IDisposable
{
    /// <summary>
    /// Raised when a partial transcription result is available during ongoing speech.
    /// The text represents only the new portion (diff) since the last partial result.
    /// </summary>
    event EventHandler<DictationResultEventArgs>? PartialResult;

    /// <summary>
    /// Raised when a speech segment has ended and the final transcription is available.
    /// The text is the complete transcription of the finalized segment.
    /// </summary>
    event EventHandler<DictationResultEventArgs>? FinalResult;

    /// <summary>
    /// Raised when an error occurs in the pipeline.
    /// </summary>
    event EventHandler<DictationErrorEventArgs>? Error;

    /// <summary>
    /// Whether the pipeline is currently running (capturing and processing audio).
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts the dictation pipeline: begins audio capture, VAD, and ASR processing.
    /// </summary>
    /// <param name="deviceIndex">Audio input device index, or -1 for default.</param>
    void Start(int deviceIndex = -1);

    /// <summary>
    /// Stops the dictation pipeline. Any in-progress speech segment is finalized.
    /// </summary>
    void Stop();
}

/// <summary>
/// Event args for pipeline errors.
/// </summary>
public sealed class DictationErrorEventArgs : EventArgs
{
    public DictationErrorEventArgs(string message, Exception? exception = null)
    {
        Message = message;
        Exception = exception;
    }

    public string Message { get; }
    public Exception? Exception { get; }
}
