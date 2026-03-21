using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WhisperHeim.Services.Audio;
using WhisperHeim.Services.CallTranscription;
using WhisperHeim.Services.Dictation;
using WhisperHeim.Services.FileTranscription;
using WhisperHeim.Services.Transcription;
using WhisperHeim.Services.Hotkey;
using WhisperHeim.Services.Input;
using WhisperHeim.Services.Models;
using WhisperHeim.Services.Orchestration;
using WhisperHeim.Services.Settings;
using WhisperHeim.Services.Templates;
using WhisperHeim.Views;
using WhisperHeim.Views.Pages;
using Wpf.Ui.Controls;

namespace WhisperHeim;

/// <summary>
/// Main settings window that lives in the system tray.
/// </summary>
public partial class MainWindow : FluentWindow
{
    private bool _isExiting;
    private readonly SettingsService _settingsService;
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly ModelManagerService _modelManager;
    private readonly ITranscriptionService _transcriptionService;
    private readonly IInputSimulator _inputSimulator;
    private readonly ITemplateService _templateService;

    // Transcript storage for the Transcripts page
    private readonly ITranscriptStorageService _transcriptStorageService = new TranscriptStorageService();

    // File transcription for the Transcribe Files page
    private readonly IFileTranscriptionService _fileTranscriptionService;

    // Hotkey and orchestration
    private readonly GlobalHotkeyService _hotkeyService = new();
    private DictationOrchestrator? _orchestrator;

    // Template hotkey and orchestration (separate from dictation) -- disabled for now
    // TODO: re-enable template orchestrator with hold-to-talk pattern

    // Tray icon images
    private ImageSource? _idleIcon;
    private ImageSource? _recordingIcon;

    // Dictation overlay indicator
    private DictationOverlayWindow? _overlayWindow;

    // Cache pages so they are not recreated on every navigation
    private readonly Dictionary<string, object> _pageCache = new();

    public MainWindow(
        SettingsService settingsService,
        IAudioCaptureService audioCaptureService,
        ModelManagerService modelManager,
        ITranscriptionService transcriptionService,
        IInputSimulator inputSimulator,
        IFileTranscriptionService fileTranscriptionService,
        ITemplateService templateService)
    {
        _settingsService = settingsService;
        _audioCaptureService = audioCaptureService;
        _modelManager = modelManager;
        _transcriptionService = transcriptionService;
        _inputSimulator = inputSimulator;
        _fileTranscriptionService = fileTranscriptionService;
        _templateService = templateService;

        InitializeComponent();

        // Generate tray icons for idle and recording states
        _idleIcon = CreateMicrophoneIcon(Brushes.White);
        _recordingIcon = CreateMicrophoneIcon(Brushes.Red);
        TrayIcon.Icon = _idleIcon;

        // Start minimized to tray - don't show the window
        Visibility = Visibility.Hidden;
        ShowInTaskbar = false;

        // Low-level keyboard hooks don't need an HWND, so we can set up immediately
        SetupHotkeysAndOrchestration();
    }

    private void SetupHotkeysAndOrchestration()
    {
        // Register the global dictation hotkey (low-level keyboard hook, no HWND needed)
        bool registered = _hotkeyService.Register();
        if (!registered)
        {
            Trace.TraceWarning(
                "[MainWindow] Failed to register global hotkey. " +
                "Another application may own the combination.");
        }

        // Wire up the hold-to-talk orchestrator
        _orchestrator = new DictationOrchestrator(
            _hotkeyService,
            _audioCaptureService,
            _transcriptionService,
            _inputSimulator,
            OnDictationStateChanged);

        // Wire up audio amplitude for overlay RMS visualization
        _orchestrator.AudioAmplitudeChanged += OnAudioAmplitudeChanged;

        // Wire up pipeline errors for overlay error state
        _orchestrator.PipelineError += OnPipelineError;

        _orchestrator.Start();

        // Initialize the dictation overlay if enabled in settings
        InitializeOverlay();

        Trace.TraceInformation(
            "[MainWindow] Orchestrator started. Dictation hotkey: {0}",
            registered);
    }

    /// <summary>
    /// Creates and configures the dictation overlay window based on settings.
    /// </summary>
    private void InitializeOverlay()
    {
        var overlaySettings = _settingsService.Current.Overlay;
        if (!overlaySettings.Enabled)
        {
            Trace.TraceInformation("[MainWindow] Overlay disabled in settings.");
            return;
        }

        _overlayWindow = new DictationOverlayWindow();
        _overlayWindow.ApplySettings(overlaySettings);

        Trace.TraceInformation("[MainWindow] Overlay initialized. Position: {0}, Size: {1}",
            overlaySettings.Position, overlaySettings.Size);
    }

    /// <summary>
    /// Callback from the orchestrator when dictation starts or stops.
    /// Updates the tray icon and overlay to reflect the current state.
    /// Called on the UI thread.
    /// </summary>
    private void OnDictationStateChanged(bool isActive)
    {
        TrayIcon.Icon = (isActive ? _recordingIcon : _idleIcon)!;
        TrayIcon.TooltipText = isActive ? "WhisperHeim - Recording..." : "WhisperHeim";

        if (isActive)
        {
            _overlayWindow?.ShowOverlay();
            // Start in Idle (listening) state; will transition to Speaking when audio amplitude rises
            _overlayWindow?.SetMicState(Views.OverlayMicState.Idle);
        }
        else
        {
            _overlayWindow?.HideOverlay();
        }

        Trace.TraceInformation("[MainWindow] Tray icon updated. Active: {0}", isActive);
    }

    /// <summary>
    /// Callback from the orchestrator with real-time audio RMS amplitude.
    /// Called on a background thread -- dispatches to UI thread.
    /// </summary>
    private void OnAudioAmplitudeChanged(double rmsAmplitude)
    {
        // Use a simple threshold to detect speech vs idle
        const double speechThreshold = 0.015;

        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            if (_overlayWindow is null) return;

            if (rmsAmplitude > speechThreshold)
            {
                _overlayWindow.SetMicState(Views.OverlayMicState.Speaking);
                _overlayWindow.UpdateAmplitude(rmsAmplitude);
            }
            else
            {
                _overlayWindow.SetMicState(Views.OverlayMicState.Idle);
            }
        });
    }

    /// <summary>
    /// Callback from the orchestrator when a pipeline error occurs.
    /// Called on a background thread -- dispatches to UI thread.
    /// </summary>
    private void OnPipelineError(Exception ex)
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            _overlayWindow?.SetMicState(Views.OverlayMicState.Error);
            Trace.TraceError("[MainWindow] Pipeline error reflected in overlay: {0}", ex.Message);
        });
    }

    /// <summary>
    /// Renders a microphone glyph from Segoe Fluent Icons into a BitmapSource
    /// suitable for use as a tray icon.
    /// </summary>
    private static ImageSource CreateMicrophoneIcon(Brush foreground)
    {
        const int size = 32;
        // U+E720 = Microphone glyph in Segoe Fluent Icons / Segoe MDL2 Assets
        const string microphoneGlyph = "\uE720";

        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            var typeface = new Typeface(
                new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

            var text = new FormattedText(
                microphoneGlyph,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                24,
                foreground,
                96);

            // Center the glyph
            var x = (size - text.Width) / 2;
            var y = (size - text.Height) / 2;
            ctx.DrawText(text, new System.Windows.Point(x, y));
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// Shows a brief notification when a template is matched (or not).
    /// Called on the UI thread.
    /// </summary>
    private void ShowTemplateNotification(string message)
    {
        TrayIcon.TooltipText = message;
        Trace.TraceInformation("[MainWindow] Template notification: {0}", message);

        // Reset tooltip after a few seconds
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            TrayIcon.TooltipText = "WhisperHeim";
        };
        timer.Start();
    }

    /// <summary>
    /// Intercept window closing to hide to tray instead of actually closing,
    /// unless the user chose Exit from the tray menu.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            Hide();
            ShowInTaskbar = false;
        }
        else
        {
            // Clean up orchestrator, overlay, and hotkey on actual exit
            _overlayWindow?.Close();
            _orchestrator?.Dispose();
            _hotkeyService.Dispose();
        }

        base.OnClosing(e);
    }

    private void NotifyIcon_LeftClick(object sender, RoutedEventArgs e)
    {
        ToggleWindowVisibility();
    }

    private void TraySettings_Click(object sender, RoutedEventArgs e)
    {
        ShowWindow();
    }

    private void TrayExit_Click(object sender, RoutedEventArgs e)
    {
        _isExiting = true;
        Application.Current.Shutdown();
    }

    private void ToggleWindowVisibility()
    {
        if (IsVisible)
        {
            Hide();
            ShowInTaskbar = false;
        }
        else
        {
            ShowWindow();
        }
    }

    /// <summary>
    /// Shows the settings window. Called from App.xaml.cs on manual (non-minimized) launch.
    /// </summary>
    public void ShowSettingsWindow() => ShowWindow();

    private void ShowWindow()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();
    }

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Guard: SelectionChanged fires during InitializeComponent before controls are ready
        if (PageContent is null) return;

        if (NavList.SelectedItem is ListBoxItem item && item.Tag is string tag)
        {
            NavigateTo(tag);
        }
    }

    private void NavigateTo(string pageName)
    {
        if (!_pageCache.TryGetValue(pageName, out var page))
        {
            page = pageName switch
            {
                "General" => new GeneralPage(_settingsService),
                "Dictation" => new DictationPage(_settingsService, _audioCaptureService),
                "Templates" => new TemplatesPage(_templateService),
                "TranscribeFiles" => new TranscribeFilesPage(_fileTranscriptionService),
                "Transcripts" => new TranscriptsPage(_transcriptStorageService),
                "About" => new AboutPage(_modelManager),
                _ => null
            };

            if (page is not null)
            {
                _pageCache[pageName] = page;
            }
        }

        if (page is not null)
        {
            PageContent.Content = page;
        }
    }
}
