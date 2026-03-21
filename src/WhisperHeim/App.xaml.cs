using System.Linq;
using System.Windows;
using WhisperHeim.Services.Audio;
using WhisperHeim.Services.Dictation;
using WhisperHeim.Services.Input;
using WhisperHeim.Services.Models;
using WhisperHeim.Services.Settings;
using WhisperHeim.Services.Startup;
using WhisperHeim.Services.FileTranscription;
using WhisperHeim.Services.Templates;
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

    private void OnStartup(object sender, StartupEventArgs e)
    {
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

        // Create services for the dictation pipeline
        var vadService = new SileroVadService(ModelManagerService.SileroVadModelPath);
        var transcriptionService = new TranscriptionService();
        transcriptionService.LoadModel();
        var dictationPipeline = new DictationPipeline(
            _audioCaptureService, vadService, transcriptionService);
        var inputSimulator = new InputSimulator();
        var fileTranscriptionService = new FileTranscriptionService(transcriptionService);
        var templateService = new TemplateService(_settingsService);

        // Determine whether we were launched via auto-start (--minimized flag)
        var startMinimized = e.Args.Contains("--minimized");

        // Create the main window with all services
        var mainWindow = new MainWindow(
            _settingsService,
            _audioCaptureService,
            _modelManager,
            dictationPipeline,
            inputSimulator,
            fileTranscriptionService,
            templateService);
        MainWindow = mainWindow;

        if (!startMinimized)
        {
            mainWindow.ShowSettingsWindow();
        }
    }
}
