using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using WhisperHeim.Services.Recording;
using WhisperHeim.Services.Transcription;

namespace WhisperHeim.Services.CallTranscription;

/// <summary>
/// Headless service that auto-enqueues call-recording sessions for transcription
/// as soon as they stop. Owned by App so the path runs even when no window has
/// been constructed (the start-minimized → tray-only flow).
///
/// <para>
/// Historically, <c>TranscriptsPage</c> itself subscribed to
/// <see cref="ICallRecordingService.RecordingStopped"/> and called
/// <c>TranscriptionQueueService.Enqueue</c>. That coupled auto-transcription to
/// the page lifetime, which forced MainWindow (and its tray icon) to be
/// eagerly constructed at startup. Extracting it here lets MainWindow be
/// constructed lazily on first open while still guaranteeing every recording
/// gets queued.
/// </para>
///
/// <para>
/// When the page is open it still applies title and speaker-name edits in its
/// own <c>OnRecordingStopped</c> handler — but those mutations happen on the
/// <see cref="CallRecordingSession"/> instance before the event is raised, so
/// they're visible to this service via <see cref="CallRecordingSession.Title"/>
/// and <see cref="CallRecordingSession.RemoteSpeakerNames"/>.
/// </para>
/// </summary>
public sealed class AutoTranscriptionService : IDisposable
{
    private readonly ICallRecordingService _recordingService;
    private readonly TranscriptionQueueService _queueService;
    private bool _disposed;

    public AutoTranscriptionService(
        ICallRecordingService recordingService,
        TranscriptionQueueService queueService)
    {
        _recordingService = recordingService;
        _queueService = queueService;
        _recordingService.RecordingStopped += OnRecordingStopped;
    }

    private void OnRecordingStopped(object? sender, CallRecordingStoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            Trace.TraceWarning(
                "[AutoTranscription] Recording stopped with error, skipping auto-enqueue: {0}",
                e.Exception.Message);
            return;
        }

        // Defer the enqueue to Background priority so any UI subscribers
        // (TranscriptsPage's RecordingStopped handler runs at Normal priority
        // and mutates session.Title / session.RemoteSpeakerNames from the
        // drawer's edit fields) finish first. When no page is open — the
        // start-minimized flow — this just runs immediately with defaults.
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(DispatcherPriority.Background, () => EnqueueWithDefaults(e.Session));
        }
        else if (dispatcher is not null)
        {
            dispatcher.BeginInvoke(DispatcherPriority.Background, () => EnqueueWithDefaults(e.Session));
        }
        else
        {
            EnqueueWithDefaults(e.Session);
        }
    }

    private void EnqueueWithDefaults(CallRecordingSession session)
    {
        var title = !string.IsNullOrWhiteSpace(session.Title)
            ? session.Title!
            : $"Call {session.StartTimestamp.LocalDateTime:yyyy-MM-dd HH:mm}";

        _queueService.Enqueue(title, session);
        Trace.TraceInformation("[AutoTranscription] Auto-enqueued recording: {0}", title);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _recordingService.RecordingStopped -= OnRecordingStopped;
    }
}
