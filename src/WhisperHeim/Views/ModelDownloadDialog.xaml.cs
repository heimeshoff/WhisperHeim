using System.Windows;
using WhisperHeim.Services.Models;

namespace WhisperHeim.Views;

/// <summary>
/// Dialog that shows model download progress with cancellation support.
/// Call <see cref="ShowAndDownloadAsync"/> to display and run the download.
/// </summary>
public partial class ModelDownloadDialog : Window
{
    private readonly ModelManagerService _modelManager;
    private CancellationTokenSource? _cts;

    /// <summary>True if all downloads completed successfully.</summary>
    public bool DownloadSucceeded { get; private set; }

    /// <summary>True if the user cancelled the download.</summary>
    public bool WasCancelled { get; private set; }

    public ModelDownloadDialog(ModelManagerService modelManager)
    {
        _modelManager = modelManager;
        InitializeComponent();

        Loaded += OnLoaded;
    }

    /// <summary>
    /// Shows the dialog and downloads all missing models.
    /// Returns true if all downloads completed successfully.
    /// </summary>
    public static bool ShowAndDownload(ModelManagerService modelManager)
    {
        var dialog = new ModelDownloadDialog(modelManager);
        dialog.ShowDialog();
        return dialog.DownloadSucceeded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();

        var progress = new Progress<ModelDownloadProgress>(OnProgress);

        try
        {
            await _modelManager.DownloadAllMissingModelsAsync(progress, _cts.Token);
            DownloadSucceeded = true;
            StatusText.Text = "All models downloaded successfully.";
            DownloadProgress.Value = 100;
            CancelButton.Content = "Close";
        }
        catch (OperationCanceledException)
        {
            WasCancelled = true;
            StatusText.Text = "Download cancelled.";
            CancelButton.Content = "Close";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Download failed: {ex.Message}";
            CancelButton.Content = "Close";
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
        }
    }

    private void OnProgress(ModelDownloadProgress p)
    {
        StatusText.Text = $"Downloading {p.ModelName}...";

        var downloadedMB = p.TotalDownloaded / (1024.0 * 1024.0);
        var totalMB = p.TotalExpected / (1024.0 * 1024.0);

        FileDetailText.Text = $"{p.CurrentFileName} — file {p.FileIndex + 1} of {p.FileCount} " +
                              $"({downloadedMB:F1} / {totalMB:F1} MB)";

        DownloadProgress.Value = p.OverallPercent;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_cts is not null)
        {
            _cts.Cancel();
            CancelButton.IsEnabled = false;
            StatusText.Text = "Cancelling...";
        }
        else
        {
            // Download finished or failed -- close the dialog
            Close();
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // If download is in progress, cancel it instead of closing immediately
        if (_cts is not null)
        {
            e.Cancel = true;
            _cts.Cancel();
            StatusText.Text = "Cancelling...";
            CancelButton.IsEnabled = false;
        }

        base.OnClosing(e);
    }
}
