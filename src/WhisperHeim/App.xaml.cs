using System.Diagnostics;
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
using WhisperHeim.Services.SelectedText;
using WhisperHeim.Services.TextToSpeech;
using WhisperHeim.Services.Transcription;
using WhisperHeim.Views;
using Wpf.Ui.Appearance;

namespace WhisperHeim;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly DataPathService _dataPathService = new();
    private SettingsService? _settingsService;
    private readonly AudioCaptureService _audioCaptureService = new();
    private readonly ModelManagerService _modelManager = new();
    private ReadAloudHotkeyService? _readAloudHotkeyService;
    private bool _isShowingError;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Run as headless diarization worker if launched with --diarize-worker.
        // This must happen before any WPF initialization.
        if (e.Args.Length > 0 && e.Args[0] == "--diarize-worker")
        {
            Services.Diarization.DiarizationWorker.Run(e.Args);
            Shutdown(0);
            return;
        }

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
            System.Diagnostics.Trace.TraceError(
                "[App] Unhandled domain exception (IsTerminating={0}): {1}\nStackTrace: {2}",
                args.IsTerminating, ex?.Message, ex?.StackTrace ?? "(no stack trace)");

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

        // Load bootstrap config (data path pointer + machine-local settings)
        _dataPathService.Load();

        // Enable trace output to a log file for diagnostics
        var logPath = _dataPathService.LogPath;
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
        Trace.Listeners.Add(new TextWriterTraceListener(logPath) { TraceOutputOptions = TraceOptions.DateTime });
        Trace.AutoFlush = true;
        Trace.TraceInformation("[App] WhisperHeim starting...");
        Trace.TraceInformation("[App] Data path: {0}", _dataPathService.DataPath);

        // Run migration from old flat structure to new per-session structure
        _dataPathService.MigrateIfNeeded();

        // Initialize path-dependent static services
        ModelManagerService.Initialize(_dataPathService);
        TextToSpeechService.Initialize(_dataPathService);
        HighQualityLoopbackService.Initialize(_dataPathService);

        // Load settings (creates file with defaults on first run)
        _settingsService = new SettingsService(_dataPathService);
        _settingsService.Load();

        // Apply the persisted theme so the UI matches the user's last choice
        var savedTheme = _settingsService.Current.General.Theme;
        if (savedTheme == "System")
        {
            ApplicationThemeManager.ApplySystemTheme();
        }
        else
        {
            var appTheme = savedTheme == "Dark" ? ApplicationTheme.Dark : ApplicationTheme.Light;
            ApplicationThemeManager.Apply(appTheme);
        }

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

        // Create call recording services — recordings go directly to the data path
        var callRecordingService = new CallRecordingService(_dataPathService);
        var transcriptStorageService = new TranscriptStorageService(_dataPathService);
        var speakerDiarizationService = new SpeakerDiarizationService();
        var callTranscriptionPipeline = new CallTranscriptionPipeline(
            speakerDiarizationService, transcriptionService, transcriptStorageService);
        var callRecordingHotkeyService = new CallRecordingHotkeyService(callRecordingService);

        // Create transcription queue service (replaces TranscriptionBusyService)
        var transcriptionQueueService = new TranscriptionQueueService(
            callTranscriptionPipeline,
            fileTranscriptionService,
            transcriptStorageService,
            () => _settingsService!.Current.General.DefaultSpeakerName);

        // Create text-to-speech service (lazy-loaded: model loads on first use)
        var textToSpeechService = new TextToSpeechService();

        // Create high-quality loopback service for voice cloning from system audio
        var highQualityLoopbackService = new HighQualityLoopbackService();

        // Create high-quality mic recorder for voice cloning
        var highQualityRecorderService = new HighQualityRecorderService();

        // Create read-aloud services (selected text capture + hotkey)
        // NOTE: must be stored in a field to prevent GC from collecting the keyboard hook delegate
        var selectedTextService = new SelectedTextService();
        _readAloudHotkeyService = new ReadAloudHotkeyService(selectedTextService, _settingsService);
        _readAloudHotkeyService.Register();

        // Check the user's "Start Minimized" setting
        var startMinimized = _settingsService.Current.General.StartMinimized;

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
            transcriptStorageService,
            highQualityLoopbackService,
            highQualityRecorderService,
            textToSpeechService,
            _readAloudHotkeyService,
            transcriptionQueueService);
        MainWindow = mainWindow;

        if (startMinimized)
        {
            // Show the window off-screen so the tray icon renders, then hide it
            mainWindow.InitializeTrayAndHide();
        }
        else
        {
            mainWindow.ShowSettingsWindow();
        }

        // Warm up TTS voice in the background after UI is ready.
        // This pre-loads the model and runs a dummy generation to cache the
        // default voice's embedding, so the first read-aloud hotkey press is instant.
        var defaultVoiceId = _settingsService.Current.Tts.DefaultVoiceId;
        _ = Task.Run(async () =>
        {
            try
            {
                await textToSpeechService.WarmUpAsync(defaultVoiceId);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("[App] TTS warm-up failed (non-fatal): {0}", ex.Message);
            }
        });
    }
}
