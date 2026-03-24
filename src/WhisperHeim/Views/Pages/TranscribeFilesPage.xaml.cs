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
    private readonly TranscriptionBusyService _busyService;
    private readonly TranscribeFilesViewModel _viewModel = new();
    private CancellationTokenSource? _cts;
    private bool _isDragOver;

    public TranscribeFilesPage(
        IFileTranscriptionService fileTranscriptionService,
        TranscriptionBusyService busyService)
    {
        _fileTranscriptionService = fileTranscriptionService;
        _busyService = busyService;
        DataContext = _viewModel;
        InitializeComponent();

        // Bind to busy service changes to update UI
        _busyService.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(TranscriptionBusyService.IsBusy))
            {
                Dispatcher.BeginInvoke(UpdateBusyState);
            }
        };
    }

    /// <summary>
    /// Updates the drop zone and pick button to reflect the engine busy state.
    /// </summary>
    private void UpdateBusyState()
    {
        bool isBusy = _busyService.IsBusy;
        PickFilesButton.IsEnabled = !isBusy;
        AllowDrop = !isBusy;

        if (isBusy)
        {
            BusyOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            BusyOverlay.Visibility = Visibility.Collapsed;
        }
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

    private async void ProcessFiles(string[] filePaths)
    {
        try
        {
            await ProcessFilesCore(filePaths);
        }
        catch (Exception ex)
        {
            // Safety net: prevent any exception from escaping async void
            // and reaching the global DispatcherUnhandledException handler.
            Trace.TraceError("[TranscribeFilesPage] Unexpected error in ProcessFiles: {0}", ex);
        }
    }

    private async Task ProcessFilesCore(string[] filePaths)
    {
        // Filter to supported files
        var supportedFiles = filePaths
            .Where(f => _fileTranscriptionService.IsSupported(f))
            .ToList();

        if (supportedFiles.Count == 0)
        {
            return;
        }

        // Check engine availability before starting
        if (_busyService.IsBusy)
        {
            Trace.TraceWarning(
                "[TranscribeFilesPage] Engine busy ('{0}'), cannot start file transcription.",
                _busyService.BusySource);
            return;
        }

        // Create view models for each file
        var newItems = new List<TranscriptionItemViewModel>();
        foreach (var filePath in supportedFiles)
        {
            var item = new TranscriptionItemViewModel(filePath);
            _viewModel.Items.Add(item);
            newItems.Add(item);
        }

        // Cancel any previous batch
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        // Acquire the engine for the entire batch
        if (!_busyService.TryAcquire("File transcription"))
        {
            foreach (var item in newItems)
            {
                item.StatusText = "Engine busy";
                item.ErrorText = "The transcription engine is currently in use. Please try again when the active transcription completes.";
                item.HasError = true;
            }
            return;
        }

        try
        {
            // Transcribe sequentially
            foreach (var item in newItems)
            {
                if (token.IsCancellationRequested) break;

                item.StatusText = "Transcribing...";
                item.IsTranscribing = true;

                var progress = new Progress<double>(p =>
                {
                    item.Progress = p * 100;
                });

                try
                {
                    var result = await _fileTranscriptionService.TranscribeFileAsync(
                        item.FilePath, progress, token);

                    item.TranscriptText = string.IsNullOrWhiteSpace(result.Text)
                        ? "(No speech detected)"
                        : result.Text;
                    item.DurationText = $"Audio: {result.AudioDuration:mm\\:ss} | Transcribed in {result.TranscriptionDuration:mm\\:ss}";
                    item.StatusText = "Done";
                    item.HasResult = true;
                }
                catch (OperationCanceledException)
                {
                    item.StatusText = "Cancelled";
                    break;
                }
                catch (Exception ex)
                {
                    item.ErrorText = ex.Message;
                    item.HasError = true;
                    item.StatusText = "Error";
                    Trace.TraceError("[TranscribeFilesPage] Transcription failed for '{0}': {1}",
                        item.FileName, ex);
                }
                finally
                {
                    item.IsTranscribing = false;
                    item.Progress = 0;
                }
            }
        }
        finally
        {
            _busyService.Release();
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
