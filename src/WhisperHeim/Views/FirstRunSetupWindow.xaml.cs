using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using WhisperHeim.Services.Models;

namespace WhisperHeim.Views;

/// <summary>
/// First-run model download dialog. Surfaced from <see cref="App.OnStartup"/>
/// when <see cref="ModelManagerService.GetMissingRequiredModels"/> returns any
/// rows or when <c>VELOPACK_FIRSTRUN</c> is set. One row per missing model with
/// progress bar, byte counter, pause/resume button. The user can either:
///   - Wait for all downloads to finish and click <b>Continue</b>, or
///   - Click <b>Skip for now</b> to dismiss; the existing lazy-download path
///     (<see cref="ModelDownloadDialog"/>) remains as a fallback.
/// </summary>
public partial class FirstRunSetupWindow : Window
{
    private readonly ModelManagerService _modelManager;
    private readonly IReadOnlyList<ModelDefinition> _modelsToDownload;
    private readonly ObservableCollection<ModelRowVm> _rows = new();
    private CancellationTokenSource? _windowCts;
    private bool _continueRequested;
    private bool _skipRequested;

    /// <summary>True if all required models are now ready on disk.</summary>
    public bool AllModelsReady { get; private set; }

    /// <summary>True if the user clicked "Skip for now".</summary>
    public bool UserSkipped { get; private set; }

    public FirstRunSetupWindow(ModelManagerService modelManager, IReadOnlyList<ModelDefinition> missingModels)
    {
        _modelManager = modelManager;
        _modelsToDownload = missingModels;
        InitializeComponent();

        foreach (var model in _modelsToDownload)
        {
            var row = new ModelRowVm(model);
            row.ResumeRequested += OnRowResumeRequested;
            _rows.Add(row);
        }

        BuildRowsUi();
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Shows the dialog and runs downloads. Returns true when the user can
    /// proceed (either everything downloaded successfully, or they skipped --
    /// caller checks <see cref="UserSkipped"/> / <see cref="AllModelsReady"/>
    /// to differentiate).
    /// </summary>
    public static FirstRunSetupWindow ShowAndRun(
        ModelManagerService modelManager, IReadOnlyList<ModelDefinition> missingModels)
    {
        var win = new FirstRunSetupWindow(modelManager, missingModels);
        win.ShowDialog();
        return win;
    }

    private void BuildRowsUi()
    {
        ModelsList.Items.Clear();
        foreach (var row in _rows)
        {
            ModelsList.Items.Add(BuildRowView(row));
        }
    }

    private static Border BuildRowView(ModelRowVm row)
    {
        var grid = new Grid { Margin = new Thickness(0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Name (top-left)
        var nameText = new TextBlock
        {
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
        };
        nameText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        nameText.SetBinding(TextBlock.TextProperty, new Binding(nameof(ModelRowVm.DisplayName)) { Source = row });
        Grid.SetRow(nameText, 0); Grid.SetColumn(nameText, 0);
        grid.Children.Add(nameText);

        // Size estimate (top-right)
        var sizeText = new TextBlock
        {
            FontSize = 12,
            Opacity = 0.65,
            VerticalAlignment = VerticalAlignment.Center,
        };
        sizeText.SetBinding(TextBlock.TextProperty, new Binding(nameof(ModelRowVm.SizeEstimate)) { Source = row });
        Grid.SetRow(sizeText, 0); Grid.SetColumn(sizeText, 1);
        grid.Children.Add(sizeText);

        // Progress bar
        var bar = new ProgressBar
        {
            Height = 14,
            Minimum = 0,
            Maximum = 100,
        };
        // ProgressBar.Value inherits TwoWay-by-default from RangeBase, which
        // can't bind against ModelRowVm.Percent's private setter -- pin OneWay.
        bar.SetBinding(ProgressBar.ValueProperty, new Binding(nameof(ModelRowVm.Percent)) { Source = row, Mode = BindingMode.OneWay });
        Grid.SetRow(bar, 2); Grid.SetColumn(bar, 0); Grid.SetColumnSpan(bar, 2);
        grid.Children.Add(bar);

        // Status text (bytes)
        var status = new TextBlock
        {
            FontSize = 11,
            Opacity = 0.6,
        };
        status.SetBinding(TextBlock.TextProperty, new Binding(nameof(ModelRowVm.StatusLine)) { Source = row });
        Grid.SetRow(status, 4); Grid.SetColumn(status, 0);
        grid.Children.Add(status);

        // Pause/resume button
        var pauseBtn = new Button
        {
            Padding = new Thickness(12, 4, 12, 4),
            FontSize = 11,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        pauseBtn.SetBinding(Button.ContentProperty, new Binding(nameof(ModelRowVm.PauseButtonLabel)) { Source = row });
        pauseBtn.SetBinding(Button.IsEnabledProperty, new Binding(nameof(ModelRowVm.CanPauseOrResume)) { Source = row });
        pauseBtn.Click += (_, _) => row.TogglePause();
        Grid.SetRow(pauseBtn, 4); Grid.SetColumn(pauseBtn, 1);
        grid.Children.Add(pauseBtn);

        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 10),
            BorderThickness = new Thickness(1),
            Child = grid,
        };
        border.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
        return border;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _windowCts = new CancellationTokenSource();
        await RunAllDownloadsAsync(_windowCts.Token);
    }

    private async Task RunAllDownloadsAsync(CancellationToken ct)
    {
        HideError();

        var anyFailed = false;
        foreach (var row in _rows)
        {
            if (ct.IsCancellationRequested) return;
            if (row.IsComplete) continue;

            try
            {
                await DownloadRowAsync(row, ct);
                row.MarkComplete();
            }
            catch (OperationCanceledException) when (row.PauseRequested)
            {
                // Paused -- leave row in paused state; user can resume.
                row.OnPaused();
            }
            catch (OperationCanceledException)
            {
                // Window-wide cancel; bail out.
                return;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(
                    "[FirstRunSetup] Download failed for {0}: {1}", row.Model.Name, ex.Message);
                anyFailed = true;
                row.OnError(ex);
                ShowError(
                    $"Download of {row.Model.Name} failed: {ShortMessage(ex)}. " +
                    "Check your connection and click Retry.");
                break;
            }
        }

        UpdateContinueState();

        // If every row is ready (or this is the on-launch happy path with
        // nothing to do), persist the manifest so subsequent launches
        // fast-path past this dialog.
        if (!anyFailed && _rows.All(r => r.IsComplete))
        {
            try { _modelManager.WriteManifest(); }
            catch (Exception ex)
            {
                Trace.TraceWarning("[FirstRunSetup] WriteManifest failed: {0}", ex.Message);
            }
        }
    }

    private async Task DownloadRowAsync(ModelRowVm row, CancellationToken windowCt)
    {
        // Per-row CTS lets pause cancel just this row without killing the dialog.
        using var rowCts = CancellationTokenSource.CreateLinkedTokenSource(windowCt);
        row.AttachCancellation(rowCts);

        row.OnStarted();

        await foreach (var progress in _modelManager.EnsureModelsAsync(
            new[] { row.Model }, rowCts.Token))
        {
            row.OnProgress(progress);
        }

        row.DetachCancellation();
    }

    private void ShowError(string text)
    {
        ErrorText.Text = text;
        ErrorBanner.Visibility = Visibility.Visible;
    }

    private void HideError()
    {
        ErrorBanner.Visibility = Visibility.Collapsed;
    }

    private async void OnRowResumeRequested(object? sender, EventArgs e)
    {
        if (sender is not ModelRowVm row) return;
        if (_windowCts is null || _windowCts.IsCancellationRequested)
        {
            _windowCts?.Dispose();
            _windowCts = new CancellationTokenSource();
        }

        try
        {
            await DownloadRowAsync(row, _windowCts.Token);
            row.MarkComplete();
        }
        catch (OperationCanceledException) when (row.PauseRequested)
        {
            row.OnPaused();
        }
        catch (OperationCanceledException)
        {
            // window closing
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[FirstRunSetup] Resume failed for {0}: {1}", row.Model.Name, ex.Message);
            row.OnError(ex);
            ShowError($"Download of {row.Model.Name} failed: {ShortMessage(ex)}.");
        }
        UpdateContinueState();

        if (_rows.All(r => r.IsComplete))
        {
            try { _modelManager.WriteManifest(); }
            catch (Exception ex) { Trace.TraceWarning("[FirstRunSetup] WriteManifest failed: {0}", ex.Message); }
        }
    }

    private async void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        HideError();
        // Reset any errored rows so they get retried.
        foreach (var row in _rows)
        {
            if (row.HasError) row.ClearError();
        }
        UpdateContinueState();
        if (_windowCts is null || _windowCts.IsCancellationRequested)
        {
            _windowCts?.Dispose();
            _windowCts = new CancellationTokenSource();
        }
        await RunAllDownloadsAsync(_windowCts.Token);
    }

    private void UpdateContinueState()
    {
        ContinueButton.IsEnabled = _rows.All(r => r.IsComplete);
        AllModelsReady = _rows.All(r => r.IsComplete);
    }

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        _continueRequested = true;
        UserSkipped = false;
        Close();
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        // Cancel any in-flight downloads; partial bytes survive in .tmp
        // so a future attempt can resume.
        _skipRequested = true;
        UserSkipped = true;
        _windowCts?.Cancel();
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_continueRequested && !_skipRequested)
        {
            // User hit Esc / close box. Treat as skip.
            UserSkipped = true;
            _windowCts?.Cancel();
        }

        base.OnClosing(e);
    }

    private static string ShortMessage(Exception ex)
    {
        return ex switch
        {
            HttpRequestException hre when hre.InnerException is not null
                => hre.InnerException.Message,
            _ => ex.Message,
        };
    }

    /// <summary>
    /// Row-level view-model. INotifyPropertyChanged so the bound progress bar
    /// and status text update live. Holds per-row pause state and the
    /// per-row <see cref="CancellationTokenSource"/> used to interrupt a
    /// running download without killing the whole dialog.
    /// </summary>
    private sealed class ModelRowVm : INotifyPropertyChanged
    {
        public ModelRowVm(ModelDefinition model)
        {
            Model = model;
        }

        public ModelDefinition Model { get; }
        public string DisplayName => Model.Name;

        private double _percent;
        public double Percent
        {
            get => _percent;
            private set { _percent = value; Notify(nameof(Percent)); }
        }

        private string _statusLine = "Pending...";
        public string StatusLine
        {
            get => _statusLine;
            private set { _statusLine = value; Notify(nameof(StatusLine)); }
        }

        public string SizeEstimate
        {
            get
            {
                var mb = Model.TotalSizeBytes / (1024.0 * 1024.0);
                return mb >= 1.0 ? $"~{mb:F0} MB" : $"~{mb * 1024:F0} KB";
            }
        }

        public bool IsComplete { get; private set; }
        public bool HasError { get; private set; }
        public bool PauseRequested { get; private set; }
        public bool IsRunning { get; private set; }

        private string _pauseLabel = "Pause";
        public string PauseButtonLabel
        {
            get => _pauseLabel;
            private set { _pauseLabel = value; Notify(nameof(PauseButtonLabel)); }
        }

        private bool _canPauseOrResume;
        public bool CanPauseOrResume
        {
            get => _canPauseOrResume;
            private set { _canPauseOrResume = value; Notify(nameof(CanPauseOrResume)); }
        }

        private CancellationTokenSource? _rowCts;

        public void AttachCancellation(CancellationTokenSource cts)
        {
            _rowCts = cts;
            IsRunning = true;
            CanPauseOrResume = true;
            PauseButtonLabel = "Pause";
        }

        public void DetachCancellation()
        {
            _rowCts = null;
            IsRunning = false;
            CanPauseOrResume = false;
        }

        public void OnStarted()
        {
            HasError = false;
            PauseRequested = false;
            StatusLine = "Starting...";
        }

        public void OnProgress(ModelDownloadProgress p)
        {
            var totalBytes = p.TotalExpected;
            var done = p.TotalDownloaded;
            Percent = totalBytes > 0
                ? Math.Min(100.0, (double)done / totalBytes * 100.0)
                : 0.0;

            var doneMb = done / (1024.0 * 1024.0);
            var totalMb = totalBytes / (1024.0 * 1024.0);
            StatusLine = totalMb >= 1.0
                ? $"{doneMb:F1} / {totalMb:F1} MB  ·  {p.CurrentFileName} ({p.FileIndex + 1}/{p.FileCount})"
                : $"{done / 1024.0:F0} / {totalBytes / 1024.0:F0} KB  ·  {p.CurrentFileName}";
        }

        public void MarkComplete()
        {
            IsComplete = true;
            HasError = false;
            Percent = 100.0;
            StatusLine = "Ready";
            CanPauseOrResume = false;
            IsRunning = false;
        }

        public void OnPaused()
        {
            PauseRequested = false;
            IsRunning = false;
            PauseButtonLabel = "Resume";
            CanPauseOrResume = true;
            StatusLine = $"{StatusLine} (paused)";
        }

        public void OnError(Exception ex)
        {
            HasError = true;
            IsRunning = false;
            CanPauseOrResume = false;
            StatusLine = $"Error: {ex.Message}";
        }

        public void ClearError()
        {
            HasError = false;
            StatusLine = "Pending...";
            Percent = 0;
        }

        public void TogglePause()
        {
            if (IsRunning)
            {
                // Pause the active download. The HttpClient read loop is
                // cancelled; any partial bytes are kept in the .tmp file
                // and the next call resumes via HTTP Range.
                PauseRequested = true;
                CanPauseOrResume = false;
                PauseButtonLabel = "Resuming...";
                _rowCts?.Cancel();
            }
            else if (!IsComplete)
            {
                // Resume: re-issue the download for this row only.
                Resume();
            }
        }

        private void Resume()
        {
            // Re-enters the download for this single row. The .tmp file is
            // intact on disk and EnsureModelsAsync's HTTP Range request
            // picks up where we paused -- or restarts from byte 0 if the
            // CDN didn't honor Range.
            CanPauseOrResume = false;
            StatusLine = "Resuming...";
            ResumeRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Fired when this row should be resumed. The window owns the
        /// cancellation token lifecycle and re-runs the download for this
        /// row only.
        /// </summary>
        public event EventHandler? ResumeRequested;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
