using System.Diagnostics;
using System.Linq;
using System.Windows;
using WhisperHeim.Services.Audio;
using WhisperHeim.Services.CallTranscription;
using WhisperHeim.Services.Diarization;
using WhisperHeim.Services.Input;
using WhisperHeim.Services.Models;
using WhisperHeim.Services.Recording;
using WhisperHeim.Services.Settings;
using WhisperHeim.Services.Startup;
using WhisperHeim.Services.FileTranscription;
using WhisperHeim.Services.Templates;
using WhisperHeim.Services.TextToSpeech;
using WhisperHeim.Services.Transcription;
using WhisperHeim.Views;

namespace WhisperHeim;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly SettingsService _settingsService = new();
    private readonly AudioCaptureService _audioCaptureService = new();
    private readonly ModelManagerService _modelManager = new();
    private bool _isShowingError;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Global exception handler for diagnostics -- guarded against re-entrance
        // to prevent cascading MessageBox dialogs when multiple exceptions fire
        // (e.g. COM/MediaFoundation errors during audio decode).
        DispatcherUnhandledException += (_, args) =>
        {
            System.Diagnostics.Trace.TraceError("[App] Unhandled UI exception: {0}", args.Exception);
            args.Handled = true;

            if (_isShowingError)
                return;

            _isShowingError = true;
            try
            {
                MessageBox.Show(
                    $"WhisperHeim encountered an error:\n\n{args.Exception.Message}\n\n{args.Exception.StackTrace}",
                    "WhisperHeim Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _isShowingError = false;
            }
        };

        // Prevent unobserved task exceptions (e.g. from parallel diarization) from crashing the app
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            System.Diagnostics.Trace.TraceError("[App] Unobserved task exception: {0}", args.Exception);
            args.SetObserved();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            System.Diagnostics.Trace.TraceError("[App] Unhandled domain exception: {0}", ex);

            if (_isShowingError)
                return;

            _isShowingError = true;
            try
            {
                MessageBox.Show(
                    $"WhisperHeim fatal error:\n\n{ex?.Message}\n\n{ex?.StackTrace}",
                    "WhisperHeim Fatal Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _isShowingError = false;
            }
        };

        try
        {
            StartupCore(e);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("[App] Startup failed: {0}", ex);
            MessageBox.Show(
                $"WhisperHeim failed to start:\n\n{ex.Message}\n\n{ex.StackTrace}",
                "WhisperHeim Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void StartupCore(StartupEventArgs e)
    {
        // Enable trace output to a log file for diagnostics
        var logPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WhisperHeim", "whisperheim.log");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
        Trace.Listeners.Add(new TextWriterTraceListener(logPath) { TraceOutputOptions = TraceOptions.DateTime });
        Trace.AutoFlush = true;
        Trace.TraceInformation("[App] WhisperHeim starting...");
        // Load settings (creates file with defaults on first run)
        _settingsService.Load();

        // If auto-start is enabled, refresh the registry entry so the exe path
        // stays current (handles updates that move the executable).
        var startupService = new StartupService();
        startupService.RefreshIfEnabled();

        // Check if AI models need downloading (first run)
        if (!_modelManager.AreAllModelsReady())
        {
            bool success = ModelDownloadDialog.ShowAndDownload(_modelManager);
            if (!success)
            {
                // User cancelled or download failed -- exit gracefully
                Shutdown();
                return;
            }
        }

        // Create services
        var transcriptionService = new TranscriptionService();
        transcriptionService.LoadModel();
        var inputSimulator = new InputSimulator();
        var fileTranscriptionService = new FileTranscriptionService(transcriptionService);
        var templateService = new TemplateService(_settingsService);

        // Create call recording services
        var callRecordingService = new CallRecordingService();
        var transcriptStorageService = new TranscriptStorageService();
        var speakerDiarizationService = new SpeakerDiarizationService();
        var callTranscriptionPipeline = new CallTranscriptionPipeline(
            speakerDiarizationService, transcriptionService, transcriptStorageService);
        var callRecordingHotkeyService = new CallRecordingHotkeyService(callRecordingService);

        // Create text-to-speech service (lazy-loaded: model loads on first use)
        var textToSpeechService = new TextToSpeechService();

        // Determine whether we were launched via auto-start (--minimized flag)
        var startMinimized = e.Args.Contains("--minimized");

        // Create the main window with all services
        var mainWindow = new MainWindow(
            _settingsService,
            _audioCaptureService,
            _modelManager,
            transcriptionService,
            inputSimulator,
            fileTranscriptionService,
            templateService,
            callRecordingService,
            callTranscriptionPipeline,
            callRecordingHotkeyService,
            transcriptStorageService);
        MainWindow = mainWindow;

        if (!startMinimized)
        {
            mainWindow.ShowSettingsWindow();
        }
    }
}
