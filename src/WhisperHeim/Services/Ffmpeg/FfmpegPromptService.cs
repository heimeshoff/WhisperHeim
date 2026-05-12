using System.Diagnostics;
using System.Windows;

namespace WhisperHeim.Services.Ffmpeg;

/// <summary>
/// WPF implementation of <see cref="IFfmpegPromptService"/>. Marshals to the
/// Application dispatcher, opens <c>FfmpegMissingDialog</c> modally, and
/// reflects the dialog outcome back to the (typically background-thread) caller.
/// </summary>
public sealed class FfmpegPromptService : IFfmpegPromptService
{
    private readonly FfmpegDetector _detector;
    private readonly object _gate = new();
    // Single-flight guard — if two background services both notice FFmpeg is
    // missing in the same instant, we only want to show one dialog.
    private TaskCompletionSource<FfmpegPromptResult>? _inFlight;

    public FfmpegPromptService(FfmpegDetector detector)
    {
        _detector = detector;
    }

    public Task<FfmpegPromptResult> PromptForInstallAsync(string reason, CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<FfmpegPromptResult> tcs;
        bool weStartedIt;

        lock (_gate)
        {
            if (_inFlight is not null && !_inFlight.Task.IsCompleted)
            {
                // Piggy-back on the existing dialog. Caller will see whatever
                // result the first opener gets.
                return _inFlight.Task;
            }

            tcs = new TaskCompletionSource<FfmpegPromptResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _inFlight = tcs;
            weStartedIt = true;
        }

        if (!weStartedIt)
        {
            // Should be unreachable given the lock above, but defensive.
            return tcs.Task;
        }

        var app = Application.Current;
        if (app is null)
        {
            // No WPF app — non-UI test harness, --diarize-worker, etc.
            // Just say "cancelled" so the caller surfaces its own message.
            tcs.TrySetResult(FfmpegPromptResult.Cancelled);
            ClearInFlight(tcs);
            return tcs.Task;
        }

        app.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                // Prefer an Owner that's actually visible (the settings window),
                // not the hidden TrayIconHost stub that owns Application.MainWindow.
                // Falling back to CenterScreen positioning when no visible window
                // exists.
                var owner = ResolveVisibleOwner(app);
                var dlg = new Views.FfmpegMissingDialog(_detector, reason);
                if (owner is not null)
                {
                    dlg.Owner = owner;
                }
                else
                {
                    dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                dlg.ShowDialog();
                tcs.TrySetResult(dlg.Result);
            }
            catch (Exception ex)
            {
                Trace.TraceError("[FfmpegPromptService] Dialog failed: {0}", ex);
                tcs.TrySetResult(FfmpegPromptResult.Cancelled);
            }
            finally
            {
                ClearInFlight(tcs);
            }
        }));

        return tcs.Task;
    }

    private void ClearInFlight(TaskCompletionSource<FfmpegPromptResult> tcs)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_inFlight, tcs)) _inFlight = null;
        }
    }

    private static Window? ResolveVisibleOwner(Application app)
    {
        foreach (Window w in app.Windows)
        {
            if (w.IsVisible && w.WindowState != WindowState.Minimized)
                return w;
        }
        return null;
    }
}
