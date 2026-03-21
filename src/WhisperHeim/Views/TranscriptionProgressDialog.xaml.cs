using System.Diagnostics;
using System.Windows;
using WhisperHeim.Services.CallTranscription;
using WhisperHeim.Services.Recording;

namespace WhisperHeim.Views;

/// <summary>
/// Dialog that shows call transcription pipeline progress with cancellation support.
/// The pipeline runs on a background thread; progress is marshalled to the UI thread
/// via <see cref="IProgress{T}"/>.
/// </summary>
public partial class TranscriptionProgressDialog : Window
{
    private readonly ICallTranscriptionPipeline _pipeline;
    private readonly CallRecordingSession _session;
    private CancellationTokenSource? _cts;

    /// <summary>The completed transcript, or null if cancelled/failed.</summary>
    public CallTranscript? Result { get; private set; }

    /// <summary>True if the pipeline completed successfully.</summary>
    public bool Succeeded { get; private set; }

    /// <summary>True if the user cancelled the pipeline.</summary>
    public bool WasCancelled { get; private set; }

    public TranscriptionProgressDialog(
        ICallTranscriptionPipeline pipeline,
        CallRecordingSession session)
    {
        _pipeline = pipeline;
        _session = session;

        InitializeComponent();
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Shows the dialog, runs the pipeline, and returns the transcript (or null on cancel/error).
    /// </summary>
    public static CallTranscript? ShowAndProcess(
        ICallTranscriptionPipeline pipeline,
        CallRecordingSession session)
    {
        var dialog = new TranscriptionProgressDialog(pipeline, session);
        dialog.ShowDialog();
        return dialog.Result;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();
        var progress = new Progress<TranscriptionPipelineProgress>(OnProgress);

        try
        {
            Result = await Task.Run(
                () => _pipeline.ProcessAsync(_session, progress, _cts.Token),
                _cts.Token);

            Succeeded = true;
            StageText.Text = "Transcription complete.";
            StageProgress.Value = 100;
            OverallProgress.Value = 100;

            Trace.TraceInformation("[TranscriptionProgressDialog] Pipeline completed successfully.");

            // Auto-close on success
            Close();
        }
        catch (OperationCanceledException)
        {
            WasCancelled = true;
            StageText.Text = "Transcription cancelled.";
            DetailText.Text = "";
            CancelButton.Content = "Close";
            Trace.TraceInformation("[TranscriptionProgressDialog] Pipeline cancelled by user.");
        }
        catch (Exception ex)
        {
            StageText.Text = $"Transcription failed: {ex.Message}";
            DetailText.Text = "";
            CancelButton.Content = "Close";
            Trace.TraceError("[TranscriptionProgressDialog] Pipeline error: {0}", ex.Message);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
        }
    }

    private void OnProgress(TranscriptionPipelineProgress p)
    {
        var stageName = p.Stage switch
        {
            PipelineStage.LoadingAudio => "Loading Audio",
            PipelineStage.Diarizing => "Diarizing Speakers",
            PipelineStage.Transcribing => "Transcribing Speech",
            PipelineStage.Assembling => "Assembling Transcript",
            PipelineStage.Saving => "Saving Transcript",
            PipelineStage.Completed => "Complete",
            _ => p.Stage.ToString(),
        };

        StageText.Text = $"{stageName}...";
        DetailText.Text = p.Description;
        StageProgress.Value = p.StagePercent;
        OverallProgress.Value = p.OverallPercent;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_cts is not null)
        {
            _cts.Cancel();
            CancelButton.IsEnabled = false;
            StageText.Text = "Cancelling...";
        }
        else
        {
            // Pipeline finished or failed -- close the dialog
            Close();
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // If pipeline is in progress, cancel it instead of closing immediately
        if (_cts is not null)
        {
            e.Cancel = true;
            _cts.Cancel();
            StageText.Text = "Cancelling...";
            CancelButton.IsEnabled = false;
        }

        base.OnClosing(e);
    }
}
