using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WhisperHeim.Services.Transcription;

namespace WhisperHeim.Views.Controls;

/// <summary>
/// Persistent bottom bar that shows transcription queue status.
/// Collapsed by default (one line), expandable to show full queue.
/// </summary>
public partial class TranscriptionBottomBar : UserControl
{
    private TranscriptionQueueService? _queueService;
    private bool _isExpanded;

    public TranscriptionBottomBar()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Binds the bottom bar to the queue service. Called once from MainWindow.
    /// </summary>
    public void Initialize(TranscriptionQueueService queueService)
    {
        _queueService = queueService;
        QueueItemsList.ItemsSource = _queueService.Items;

        _queueService.PropertyChanged += OnQueuePropertyChanged;
        _queueService.Items.CollectionChanged += OnItemsCollectionChanged;

        UpdateCollapsedBar();
    }

    private void OnQueuePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(UpdateCollapsedBar);
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            UpdateCollapsedBar();
            UpdateClearButton();
        });
    }

    private void UpdateCollapsedBar()
    {
        if (_queueService is null) return;

        StatusText.Text = _queueService.StatusText;

        if (_queueService.ActiveItem is { } active)
        {
            StatusIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.ArrowSync24;
            StatusIcon.Opacity = 1.0;
            MiniProgress.Value = active.OverallPercent;
            MiniProgress.Visibility = Visibility.Visible;

            // Ensure bar is visible when processing starts
            Visibility = Visibility.Visible;
        }
        else
        {
            StatusIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.Checkmark24;
            StatusIcon.Opacity = 0.5;
            MiniProgress.Visibility = Visibility.Collapsed;

            // Hide the bar entirely when idle and no items
            bool hasItems = _queueService.Items.Count > 0;
            Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void UpdateClearButton()
    {
        if (_queueService is null) return;

        bool hasFinished = false;
        foreach (var item in _queueService.Items)
        {
            if (item.Stage is QueueItemStage.Completed or QueueItemStage.Failed)
            {
                hasFinished = true;
                break;
            }
        }

        ClearFinishedButton.Visibility = hasFinished
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void CollapsedBar_Click(object sender, MouseButtonEventArgs e)
    {
        _isExpanded = !_isExpanded;
        ExpandedPanel.Visibility = _isExpanded ? Visibility.Visible : Visibility.Collapsed;
        ExpandChevron.Symbol = _isExpanded
            ? Wpf.Ui.Controls.SymbolRegular.ChevronDown24
            : Wpf.Ui.Controls.SymbolRegular.ChevronUp24;
    }

    private void ClearFinished_Click(object sender, RoutedEventArgs e)
    {
        _queueService?.ClearFinished();
    }

    private void ItemAction_Click(object sender, RoutedEventArgs e)
    {
        if (_queueService is null) return;
        if (sender is not Button button) return;
        if (button.Tag is not TranscriptionQueueItem item) return;

        System.Diagnostics.Trace.TraceInformation(
            "[BottomBar] Action clicked for '{0}', stage={1}", item.Title, item.Stage);

        switch (item.Stage)
        {
            case QueueItemStage.Queued:
                _queueService.Remove(item);
                break;
            case QueueItemStage.Loading:
            case QueueItemStage.Diarizing:
            case QueueItemStage.Transcribing:
            case QueueItemStage.Assembling:
                _queueService.CancelActive();
                break;
            case QueueItemStage.Failed:
                _queueService.Retry(item);
                break;
        }
    }
}
