using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WhisperHeim.Services.FileTranscription;
using WhisperHeim.Services.Transcription;

namespace WhisperHeim.Views.Pages;

/// <summary>
/// Page for drag-and-drop file transcription with batch support.
/// Results are ephemeral (not persisted) unless the user explicitly saves.
/// </summary>
public partial class TranscribeFilesPage : UserControl
{
    private readonly IFileTranscriptionService _fileTranscriptionService;
    private readonly TranscriptionQueueService _busyService;
    private readonly TranscribeFilesViewModel _viewModel = new();
    private bool _isDragOver;

    public TranscribeFilesPage(
        IFileTranscriptionService fileTranscriptionService,
        TranscriptionQueueService busyService)
    {
        _fileTranscriptionService = fileTranscriptionService;
        _busyService = busyService;
        DataContext = _viewModel;
        InitializeComponent();

        // Hide the busy overlay — files now go through the queue
        BusyOverlay.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Used by the drop zone style trigger.
    /// </summary>
    public bool IsDragOver
    {
        get => _isDragOver;
        private set
        {
            if (_isDragOver != value)
            {
                _isDragOver = value;
                // Update drop zone visual
                DropZone.BorderBrush = value
                    ? (System.Windows.Media.Brush)FindResource("SystemAccentColorPrimaryBrush")
                    : (System.Windows.Media.Brush)FindResource("ControlStrokeColorDefaultBrush");
            }
        }
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            IsDragOver = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        IsDragOver = false;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        IsDragOver = false;
        e.Handled = true;

        if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
            e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            ProcessFiles(files);
        }
    }

    private void OnPickFilesClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select audio files to transcribe",
            Filter = "Audio files (*.ogg;*.mp3;*.m4a;*.wav)|*.ogg;*.mp3;*.m4a;*.wav|All files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            ProcessFiles(dialog.FileNames);
        }
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: TranscriptionItemViewModel item } && item.HasResult)
        {
            try
            {
                Clipboard.SetText(item.TranscriptText);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("[TranscribeFilesPage] Failed to copy to clipboard: {0}", ex.Message);
            }
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: TranscriptionItemViewModel item } && item.HasResult)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save transcript",
                FileName = Path.GetFileNameWithoutExtension(item.FileName) + ".txt",
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dialog.FileName, item.TranscriptText);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning("[TranscribeFilesPage] Failed to save transcript: {0}", ex.Message);
                }
            }
        }
    }

    private void ProcessFiles(string[] filePaths)
    {
        // Filter to supported files
        var supportedFiles = filePaths
            .Where(f => _fileTranscriptionService.IsSupported(f))
            .ToList();

        if (supportedFiles.Count == 0)
            return;

        // Enqueue each file into the transcription queue
        foreach (var filePath in supportedFiles)
        {
            var viewModel = new TranscriptionItemViewModel(filePath);
            _viewModel.Items.Add(viewModel);

            var queueItem = _busyService.EnqueueFile(filePath);

            // Track queue item progress → update the page view model
            queueItem.PropertyChanged += (_, args) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    switch (args.PropertyName)
                    {
                        case nameof(TranscriptionQueueItem.Stage):
                            viewModel.IsTranscribing = queueItem.Stage is QueueItemStage.Loading
                                or QueueItemStage.Transcribing
                                or QueueItemStage.Diarizing
                                or QueueItemStage.Assembling;
                            viewModel.StatusText = queueItem.Stage switch
                            {
                                QueueItemStage.Queued => "Queued",
                                QueueItemStage.Loading => "Loading...",
                                QueueItemStage.Transcribing => "Transcribing...",
                                QueueItemStage.Completed => "Done",
                                QueueItemStage.Failed => "Error",
                                _ => queueItem.Stage.ToString(),
                            };
                            if (queueItem.Stage == QueueItemStage.Completed)
                            {
                                viewModel.IsTranscribing = false;
                                viewModel.Progress = 100;
                                // Pick up result text if not already set
                                if (!viewModel.HasResult && !string.IsNullOrEmpty(queueItem.ResultText))
                                {
                                    viewModel.TranscriptText = queueItem.ResultText;
                                    viewModel.HasResult = true;
                                }
                            }
                            else if (queueItem.Stage == QueueItemStage.Failed)
                            {
                                viewModel.ErrorText = queueItem.ErrorMessage ?? "Transcription failed";
                                viewModel.HasError = true;
                                viewModel.IsTranscribing = false;
                                viewModel.Progress = 0;
                            }
                            break;

                        case nameof(TranscriptionQueueItem.OverallPercent):
                            viewModel.Progress = queueItem.OverallPercent;
                            break;

                        case nameof(TranscriptionQueueItem.ResultText):
                            if (!string.IsNullOrEmpty(queueItem.ResultText))
                            {
                                viewModel.TranscriptText = queueItem.ResultText;
                                viewModel.HasResult = true;
                            }
                            break;
                    }
                });
            };
        }
    }
}

/// <summary>
/// View model for the TranscribeFilesPage.
/// </summary>
public sealed class TranscribeFilesViewModel
{
    public ObservableCollection<TranscriptionItemViewModel> Items { get; } = new();
}

/// <summary>
/// View model for a single file transcription item.
/// </summary>
public sealed class TranscriptionItemViewModel : INotifyPropertyChanged
{
    private string _statusText = "Queued";
    private double _progress;
    private bool _isTranscribing;
    private string _transcriptText = string.Empty;
    private string _durationText = string.Empty;
    private bool _hasResult;
    private string _errorText = string.Empty;
    private bool _hasError;

    public TranscriptionItemViewModel(string filePath)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
    }

    public string FilePath { get; }
    public string FileName { get; }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public double Progress
    {
        get => _progress;
        set => SetField(ref _progress, value);
    }

    public bool IsTranscribing
    {
        get => _isTranscribing;
        set => SetField(ref _isTranscribing, value);
    }

    public string TranscriptText
    {
        get => _transcriptText;
        set => SetField(ref _transcriptText, value);
    }

    public string DurationText
    {
        get => _durationText;
        set => SetField(ref _durationText, value);
    }

    public bool HasResult
    {
        get => _hasResult;
        set => SetField(ref _hasResult, value);
    }

    public string ErrorText
    {
        get => _errorText;
        set => SetField(ref _errorText, value);
    }

    public bool HasError
    {
        get => _hasError;
        set => SetField(ref _hasError, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
