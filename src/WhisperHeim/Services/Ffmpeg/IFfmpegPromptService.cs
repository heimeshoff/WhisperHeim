namespace WhisperHeim.Services.Ffmpeg;

/// <summary>
/// UI-agnostic seam for services in <c>Services/</c> (e.g.
/// <c>StreamTranscriptionService</c>, <c>AudioFileDecoder</c>) to request the
/// FFmpeg install modal without taking a direct WPF dependency. Implemented in
/// <c>App.xaml.cs</c> wiring; the implementation marshals to the UI thread.
/// </summary>
public interface IFfmpegPromptService
{
    /// <summary>
    /// Surface the install modal on the UI thread. Returns when the user has
    /// dismissed the dialog (either after a successful install/re-detect, or
    /// after cancelling). The caller should then inspect
    /// <see cref="FfmpegDetector.CachedInfo"/> to decide whether to retry the
    /// original operation.
    ///
    /// <para>
    /// <paramref name="reason"/> is a short, user-facing string describing why
    /// the prompt was triggered (e.g. "YouTube transcription requires FFmpeg").
    /// </para>
    /// </summary>
    Task<FfmpegPromptResult> PromptForInstallAsync(string reason, CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of the FFmpeg install modal.
/// </summary>
public enum FfmpegPromptResult
{
    /// <summary>User cancelled / closed the dialog without installing.</summary>
    Cancelled,

    /// <summary>FFmpeg is now installed and detected. Caller may retry the original op.</summary>
    Installed,
}
