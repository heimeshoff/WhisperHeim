using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using WhisperHeim.Services.CallTranscription;
using WhisperHeim.Services.Recording;

namespace WhisperHeim.Services.Transcription;

/// <summary>
/// Item stages matching the call transcription pipeline.
/// </summary>
public enum QueueItemStage
{
    Queued,
    Loading,
    Diarizing,
    Transcribing,
    Assembling,
    Completed,
    Failed,
}

/// <summary>
/// A single item in the transcription queue.
/// </summary>
public sealed class TranscriptionQueueItem : INotifyPropertyChanged
{
    private QueueItemStage _stage = QueueItemStage.Queued;
    private double _stagePercent;
    private double _overallPercent;
    private string _stageDescription = string.Empty;
    private string? _errorMessage;
    private DateTimeOffset? _completedAt;

    public TranscriptionQueueItem(
        string title,
        CallRecordingSession session)
    {
        Id = Guid.NewGuid();
        Title = title;
        Session = session;
        EnqueuedAt = DateTimeOffset.Now;
    }

    public Guid Id { get; }
    public string Title { get; }
    public CallRecordingSession Session { get; }
    public DateTimeOffset EnqueuedAt { get; }

    public QueueItemStage Stage
    {
        get => _stage;
        set { if (_stage != value) { _stage = value; OnPropertyChanged(); } }
    }

    public double StagePercent
    {
        get => _stagePercent;
        set { if (Math.Abs(_stagePercent - value) > 0.01) { _stagePercent = value; OnPropertyChanged(); } }
    }

    public double OverallPercent
    {
        get => _overallPercent;
        set { if (Math.Abs(_overallPercent - value) > 0.01) { _overallPercent = value; OnPropertyChanged(); } }
    }

    public string StageDescription
    {
        get => _stageDescription;
        set { if (_stageDescription != value) { _stageDescription = value; OnPropertyChanged(); } }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set { if (_errorMessage != value) { _errorMessage = value; OnPropertyChanged(); } }
    }

    public DateTimeOffset? CompletedAt
    {
        get => _completedAt;
        set { if (_completedAt != value) { _completedAt = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// FIFO transcription queue that processes items sequentially in the background.
/// Replaces both <c>TranscriptionBusyService</c> and the modal <c>TranscriptionProgressDialog</c>.
/// Observable for UI binding.
/// </summary>
public sealed class TranscriptionQueueService : INotifyPropertyChanged
{
    private readonly ICallTranscriptionPipeline _pipeline;
    private readonly ITranscriptStorageService _transcriptStorage;
    private readonly Func<string> _getLocalSpeakerName;
    private TranscriptionQueueItem? _activeItem;
    private bool _isProcessing;
    private CancellationTokenSource? _activeCts;

    // External acquire/release for non-queue callers (e.g. file transcription page)
    private bool _externalBusy;
    private string _externalBusySource = string.Empty;

    /// <summary>
    /// All items in the queue (waiting, active, and recently completed/failed).
    /// Bound to UI. Must be modified on the dispatcher thread.
    /// </summary>
    public ObservableCollection<TranscriptionQueueItem> Items { get; } = new();

    /// <summary>The currently processing item, or null if idle.</summary>
    public TranscriptionQueueItem? ActiveItem
    {
        get => _activeItem;
        private set
        {
            if (_activeItem != value)
            {
                _activeItem = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsIdle));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    /// <summary>True when the engine is busy (queue processing or external acquire).</summary>
    public bool IsBusy => _activeItem is not null || _externalBusy;

    /// <summary>True when no transcription is active.</summary>
    public bool IsIdle => !IsBusy;

    /// <summary>
    /// Description of the current busy source (for backward compat with file transcription page).
    /// </summary>
    public string BusySource => _activeItem?.Title ?? _externalBusySource;

    /// <summary>Short status string for the collapsed bottom bar.</summary>
    public string StatusText
    {
        get
        {
            if (_activeItem is null)
                return "No active transcriptions";

            var pct = (int)_activeItem.OverallPercent;
            return $"Transcribing \"{_activeItem.Title}\" ({pct}%)";
        }
    }

    /// <summary>
    /// Raised after a queue item completes successfully.
    /// Carries the item so the UI can refresh transcript lists.
    /// </summary>
    public event EventHandler<TranscriptionQueueItem>? ItemCompleted;

    /// <summary>
    /// Raised after a queue item fails.
    /// </summary>
    public event EventHandler<TranscriptionQueueItem>? ItemFailed;

    /// <summary>
    /// Backward-compatible acquire for non-queue callers (e.g. file transcription).
    /// Returns false if the engine is already busy.
    /// </summary>
    public bool TryAcquire(string source)
    {
        lock (this)
        {
            if (IsBusy)
            {
                Trace.TraceWarning(
                    "[TranscriptionQueue] TryAcquire rejected for '{0}' -- engine busy.", source);
                return false;
            }

            _externalBusy = true;
            _externalBusySource = source;
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(ActiveItem));

            Trace.TraceInformation(
                "[TranscriptionQueue] Engine acquired externally by '{0}'.", source);
            return true;
        }
    }

    /// <summary>
    /// Releases an external acquire. Safe to call even if not busy.
    /// </summary>
    public void Release()
    {
        lock (this)
        {
            if (!_externalBusy) return;

            Trace.TraceInformation(
                "[TranscriptionQueue] Engine released by '{0}'.", _externalBusySource);

            _externalBusy = false;
            _externalBusySource = string.Empty;
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(ActiveItem));
        }

        // Now that the external lock is released, try processing the queue
        ProcessNext();
    }

    public TranscriptionQueueService(
        ICallTranscriptionPipeline pipeline,
        ITranscriptStorageService transcriptStorage,
        Func<string> getLocalSpeakerName)
    {
        _pipeline = pipeline;
        _transcriptStorage = transcriptStorage;
        _getLocalSpeakerName = getLocalSpeakerName;
    }

    /// <summary>
    /// Enqueues a recording session for transcription.
    /// Processing starts automatically if the engine is idle.
    /// </summary>
    public void Enqueue(string title, CallRecordingSession session)
    {
        var item = new TranscriptionQueueItem(title, session);
        DispatcherInvoke(() =>
        {
            Items.Add(item);
            OnPropertyChanged(nameof(StatusText));
        });

        Trace.TraceInformation(
            "[TranscriptionQueue] Enqueued '{0}'. Queue depth: {1}",
            title, Items.Count);

        ProcessNext();
    }

    /// <summary>
    /// Removes a queued (not yet active) item from the queue.
    /// </summary>
    public bool Remove(TranscriptionQueueItem item)
    {
        if (item.Stage != QueueItemStage.Queued)
            return false;

        bool removed = false;
        DispatcherInvoke(() => removed = Items.Remove(item));

        if (removed)
        {
            Trace.TraceInformation("[TranscriptionQueue] Removed queued item '{0}'.", item.Title);
            OnPropertyChanged(nameof(StatusText));
        }

        return removed;
    }

    /// <summary>
    /// Cancels the currently active transcription.
    /// </summary>
    public void CancelActive()
    {
        if (_activeCts is not null)
        {
            Trace.TraceInformation("[TranscriptionQueue] Cancelling active item '{0}'.", _activeItem?.Title);
            _activeCts.Cancel();
        }
    }

    /// <summary>
    /// Re-enqueues a failed item for retry (appended at the end).
    /// </summary>
    public void Retry(TranscriptionQueueItem item)
    {
        if (item.Stage != QueueItemStage.Failed)
            return;

        DispatcherInvoke(() => Items.Remove(item));

        // Create a fresh item for the retry
        Enqueue(item.Title, item.Session);
        Trace.TraceInformation("[TranscriptionQueue] Retrying '{0}'.", item.Title);
    }

    /// <summary>
    /// Removes completed or failed items from the list.
    /// </summary>
    public void ClearFinished()
    {
        DispatcherInvoke(() =>
        {
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                if (Items[i].Stage is QueueItemStage.Completed or QueueItemStage.Failed)
                    Items.RemoveAt(i);
            }
        });
    }

    private async void ProcessNext()
    {
        if (_isProcessing)
            return;

        // Don't start queue processing if an external caller has the lock
        if (_externalBusy)
            return;

        // Find the next queued item
        TranscriptionQueueItem? next = null;
        DispatcherInvoke(() =>
        {
            foreach (var item in Items)
            {
                if (item.Stage == QueueItemStage.Queued)
                {
                    next = item;
                    break;
                }
            }
        });

        if (next is null)
            return;

        _isProcessing = true;
        ActiveItem = next;

        while (next is not null)
        {
            await ProcessItem(next);

            // Find next queued item
            next = null;
            DispatcherInvoke(() =>
            {
                foreach (var item in Items)
                {
                    if (item.Stage == QueueItemStage.Queued)
                    {
                        next = item;
                        break;
                    }
                }
            });

            ActiveItem = next;
        }

        _isProcessing = false;
    }

    private async Task ProcessItem(TranscriptionQueueItem item)
    {
        _activeCts = new CancellationTokenSource();

        var progress = new Progress<TranscriptionPipelineProgress>(p =>
        {
            DispatcherInvoke(() =>
            {
                item.Stage = MapStage(p.Stage);
                item.StagePercent = p.StagePercent;
                item.OverallPercent = p.OverallPercent;
                item.StageDescription = p.Description;
                OnPropertyChanged(nameof(StatusText));
            });
        });

        Trace.TraceInformation("[TranscriptionQueue] Processing '{0}'.", item.Title);

        try
        {
            DispatcherInvoke(() =>
            {
                item.Stage = QueueItemStage.Loading;
                item.StageDescription = "Starting...";
            });

            var localSpeakerName = _getLocalSpeakerName();
            var remoteSpeakerNames = item.Session.RemoteSpeakerNames;

            await Task.Run(async () =>
                await _pipeline.ProcessAsync(
                    item.Session, remoteSpeakerNames, localSpeakerName,
                    progress, _activeCts.Token));

            DispatcherInvoke(() =>
            {
                item.Stage = QueueItemStage.Completed;
                item.OverallPercent = 100;
                item.StageDescription = "Complete";
                item.CompletedAt = DateTimeOffset.Now;
            });

            Trace.TraceInformation("[TranscriptionQueue] Completed '{0}'.", item.Title);
            ItemCompleted?.Invoke(this, item);
        }
        catch (OperationCanceledException)
        {
            DispatcherInvoke(() =>
            {
                item.Stage = QueueItemStage.Failed;
                item.ErrorMessage = "Cancelled";
                item.StageDescription = "Cancelled";
                item.CompletedAt = DateTimeOffset.Now;
            });

            Trace.TraceInformation("[TranscriptionQueue] Cancelled '{0}'.", item.Title);
            ItemFailed?.Invoke(this, item);
        }
        catch (Exception ex)
        {
            DispatcherInvoke(() =>
            {
                item.Stage = QueueItemStage.Failed;
                item.ErrorMessage = ex.Message;
                item.StageDescription = $"Failed: {ex.Message}";
                item.CompletedAt = DateTimeOffset.Now;
            });

            Trace.TraceError("[TranscriptionQueue] Failed '{0}': {1}", item.Title, ex.Message);
            ItemFailed?.Invoke(this, item);
        }
        finally
        {
            _activeCts.Dispose();
            _activeCts = null;
        }
    }

    private static QueueItemStage MapStage(PipelineStage stage) => stage switch
    {
        PipelineStage.LoadingAudio => QueueItemStage.Loading,
        PipelineStage.Diarizing => QueueItemStage.Diarizing,
        PipelineStage.Transcribing => QueueItemStage.Transcribing,
        PipelineStage.Assembling => QueueItemStage.Assembling,
        PipelineStage.Saving => QueueItemStage.Assembling,
        PipelineStage.Completed => QueueItemStage.Completed,
        _ => QueueItemStage.Loading,
    };

    private static void DispatcherInvoke(Action action)
    {
        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            if (dispatcher.CheckAccess())
                action();
            else
                dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            if (dispatcher.CheckAccess())
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            else
                dispatcher.BeginInvoke(() =>
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)));
        }
        else
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
