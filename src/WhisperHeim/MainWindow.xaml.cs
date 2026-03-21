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
using WhisperHeim.Services.Recording;
using WhisperHeim.Services.Transcription;
using WhisperHeim.Services.Hotkey;
using WhisperHeim.Services.Input;
using WhisperHeim.Services.Models;
using WhisperHeim.Services.Orchestration;
using WhisperHeim.Services.Settings;
using WhisperHeim.Services.Templates;
using WhisperHeim.Services.SelectedText;
using WhisperHeim.Services.TextToSpeech;
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

    // Call recording services (wired in App.xaml.cs, used by call recording UI)
    private readonly ICallRecordingService _callRecordingService;
    private readonly ICallTranscriptionPipeline _callTranscriptionPipeline;
    private readonly CallRecordingHotkeyService _callRecordingHotkeyService;

    // Transcript storage for the Transcripts page
    private readonly ITranscriptStorageService _transcriptStorageService;

    // File transcription for the Transcribe Files page
    private readonly IFileTranscriptionService _fileTranscriptionService;

    // High-quality loopback for voice cloning from system audio
    private readonly IHighQualityLoopbackService _highQualityLoopbackService;

    // High-quality mic recorder for voice cloning
    private readonly IHighQualityRecorderService _highQualityRecorderService;

    // Text-to-speech for the TTS page
    private readonly ITextToSpeechService _textToSpeechService;

    // Read-aloud hotkey service (for overlay lifecycle events)
    private readonly ReadAloudHotkeyService _readAloudHotkeyService;

    // Hotkey and orchestration
    private readonly GlobalHotkeyService _hotkeyService = new();
    private DictationOrchestrator? _orchestrator;

    // Template hotkey and orchestration (separate from dictation) -- disabled for now
    // TODO: re-enable template orchestrator with hold-to-talk pattern

    // Tray icon images
    private ImageSource? _idleIcon;
    private ImageSource? _recordingIcon;
    private ImageSource? _callRecordingIcon;

    // Dictation overlay indicator
    private DictationOverlayWindow? _overlayWindow;

    // Read-aloud overlay indicator
    private ReadAloudOverlayWindow? _readAloudOverlayWindow;

    // Cache pages so they are not recreated on every navigation
    private readonly Dictionary<string, object> _pageCache = new();

    public MainWindow(
        SettingsService settingsService,
        IAudioCaptureService audioCaptureService,
        ModelManagerService modelManager,
        ITranscriptionService transcriptionService,
        IInputSimulator inputSimulator,
        IFileTranscriptionService fileTranscriptionService,
        ITemplateService templateService,
        ICallRecordingService callRecordingService,
        ICallTranscriptionPipeline callTranscriptionPipeline,
        CallRecordingHotkeyService callRecordingHotkeyService,
        ITranscriptStorageService transcriptStorageService,
        IHighQualityLoopbackService highQualityLoopbackService,
        IHighQualityRecorderService highQualityRecorderService,
        ITextToSpeechService textToSpeechService,
        ReadAloudHotkeyService readAloudHotkeyService)
    {
        _settingsService = settingsService;
        _audioCaptureService = audioCaptureService;
        _modelManager = modelManager;
        _transcriptionService = transcriptionService;
        _inputSimulator = inputSimulator;
        _fileTranscriptionService = fileTranscriptionService;
        _templateService = templateService;
        _callRecordingService = callRecordingService;
        _callTranscriptionPipeline = callTranscriptionPipeline;
        _callRecordingHotkeyService = callRecordingHotkeyService;
        _transcriptStorageService = transcriptStorageService;
        _highQualityLoopbackService = highQualityLoopbackService;
        _highQualityRecorderService = highQualityRecorderService;
        _textToSpeechService = textToSpeechService;
        _readAloudHotkeyService = readAloudHotkeyService;

        InitializeComponent();

        // Generate tray icons for idle, dictation, and call recording states
        _idleIcon = CreateMicrophoneIcon(Brushes.White);
        _recordingIcon = CreateMicrophoneIcon(Brushes.Red);
        _callRecordingIcon = CreateMicrophoneIcon(Brushes.Orange);
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

        // Initialize the read-aloud overlay and wire up lifecycle events
        InitializeReadAloudOverlay();

        Trace.TraceInformation(
            "[MainWindow] Orchestrator started. Dictation hotkey: {0}",
            registered);

        // Register call recording hotkey: Ctrl+Win+R
        var callHotkey = new HotkeyRegistration(
            ModifierKeys.Control | ModifierKeys.Win,
            VirtualKey: 0x52); // 'R' key
        bool callHkRegistered = _callRecordingHotkeyService.Register(callHotkey);
        Trace.TraceInformation(
            "[MainWindow] Call recording hotkey registered: {0}", callHkRegistered);

        // Subscribe to call recording events
        _callRecordingService.RecordingStarted += OnCallRecordingStarted;
        _callRecordingService.RecordingStopped += OnCallRecordingStopped;
        _callRecordingService.DurationUpdated += OnCallRecordingDurationUpdated;
        _callRecordingService.StreamFailed += OnCallRecordingStreamFailed;
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
    /// Creates and configures the read-aloud overlay window and wires up lifecycle events.
    /// </summary>
    private void InitializeReadAloudOverlay()
    {
        var overlaySettings = _settingsService.Current.Overlay;
        if (!overlaySettings.Enabled)
        {
            Trace.TraceInformation("[MainWindow] Read-aloud overlay disabled (overlay disabled in settings).");
            return;
        }

        _readAloudOverlayWindow = new ReadAloudOverlayWindow();
        _readAloudOverlayWindow.ApplySettings(overlaySettings);

        // Subscribe to read-aloud lifecycle events
        _readAloudHotkeyService.ReadAloudStarted += OnReadAloudStarted;
        _readAloudHotkeyService.ReadAloudPlaying += OnReadAloudPlaying;
        _readAloudHotkeyService.ReadAloudCompleted += OnReadAloudCompleted;
        _readAloudHotkeyService.ReadAloudCancelled += OnReadAloudCancelled;

        Trace.TraceInformation("[MainWindow] Read-aloud overlay initialized.");
    }

    private void OnReadAloudStarted(object? sender, EventArgs e)
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            _readAloudOverlayWindow?.ShowOverlay();
        });
    }

    private void OnReadAloudPlaying(object? sender, EventArgs e)
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            _readAloudOverlayWindow?.SetState(Views.ReadAloudOverlayState.Playing);
        });
    }

    private void OnReadAloudCompleted(object? sender, EventArgs e)
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            _readAloudOverlayWindow?.HideOverlay();
        });
    }

    private void OnReadAloudCancelled(object? sender, EventArgs e)
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            _readAloudOverlayWindow?.DismissOverlay();
        });
    }

    /// <summary>
    /// Callback from the orchestrator when dictation starts or stops.
    /// Updates the tray icon and overlay to reflect the current state.
    /// Called on the UI thread.
    /// </summary>
    private void OnDictationStateChanged(bool isActive)
    {
        if (isActive)
        {
            TrayIcon.Icon = _recordingIcon!;
            TrayIcon.TooltipText = "WhisperHeim - Recording...";
        }
        else if (_callRecordingService.IsRecording)
        {
            // Dictation ended but call recording is still active -- restore call recording state
            TrayIcon.Icon = _callRecordingIcon!;
            var duration = _callRecordingService.CurrentSession?.Duration ?? TimeSpan.Zero;
            TrayIcon.TooltipText = $"WhisperHeim - Recording call ({CallRecordingService.FormatDuration(duration)})";
        }
        else
        {
            TrayIcon.Icon = _idleIcon!;
            TrayIcon.TooltipText = "WhisperHeim";
        }

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

    // ── Call Recording event handlers ────────────────────────────────────

    private void TrayCallRecording_Click(object sender, RoutedEventArgs e)
    {
        _callRecordingService.ToggleRecording();
    }

    private void OnCallRecordingStarted(object? sender, CallRecordingSession session)
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            TrayIcon.Icon = _callRecordingIcon!;
            TrayIcon.TooltipText = "WhisperHeim - Recording call (00:00)";
            CallRecordingMenuItem.Header = "Stop Call Recording (00:00)";
            Trace.TraceInformation("[MainWindow] Call recording started.");
        });
    }

    private void OnCallRecordingStopped(object? sender, CallRecordingStoppedEventArgs e)
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            TrayIcon.Icon = _idleIcon!;
            TrayIcon.TooltipText = "WhisperHeim";
            CallRecordingMenuItem.Header = "Start Call Recording";
            Trace.TraceInformation("[MainWindow] Call recording stopped.");

            // If recording stopped with an error, don't start transcription
            if (e.Exception is not null)
            {
                Trace.TraceWarning(
                    "[MainWindow] Call recording stopped with error, skipping transcription: {0}",
                    e.Exception.Message);
                return;
            }

            // Auto-trigger transcription pipeline with progress dialog
            StartPostRecordingTranscription(e.Session);
        });
    }

    private async void StartPostRecordingTranscription(CallRecordingSession session)
    {
        Trace.TraceInformation("[MainWindow] Starting post-recording transcription pipeline (background).");
        Trace.TraceInformation("[MainWindow] Session: mic={0}, system={1}",
            session.MicWavFilePath, session.SystemWavFilePath);

        // Show "transcribing..." indicator on the Transcripts page
        var transcriptsPage = GetOrCreateTranscriptsPage();
        transcriptsPage.ShowTranscribingIndicator();

        try
        {
            var transcript = await Task.Run(async () =>
            {
                try
                {
                    return await _callTranscriptionPipeline.ProcessAsync(session);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("[MainWindow] Pipeline background thread exception: {0}\n{1}",
                        ex.Message, ex.StackTrace);
                    throw;
                }
            });

            Trace.TraceInformation("[MainWindow] Transcription pipeline completed: {0} segments.",
                transcript.Segments.Count);

            // Refresh the transcripts page — new transcript will appear in the list
            transcriptsPage.HideTranscribingIndicator();
            transcriptsPage.RefreshList();
        }
        catch (Exception ex)
        {
            Trace.TraceError("[MainWindow] Transcription pipeline failed: {0}\n{1}",
                ex.Message, ex.StackTrace);
            transcriptsPage.HideTranscribingIndicator();
        }
    }

    private TranscriptsPage GetOrCreateTranscriptsPage()
    {
        if (_pageCache.TryGetValue("Recordings", out var cached) && cached is TranscriptsPage page)
            return page;

        page = new TranscriptsPage(_transcriptStorageService);
        _pageCache["Recordings"] = page;
        return page;
    }

    private void OnCallRecordingDurationUpdated(object? sender, TimeSpan duration)
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            var formatted = CallRecordingService.FormatDuration(duration);
            CallRecordingMenuItem.Header = $"Stop Call Recording ({formatted})";
            TrayIcon.TooltipText = $"WhisperHeim - Recording call ({formatted})";
        });
    }

    private void OnCallRecordingStreamFailed(object? sender, StreamFailedEventArgs e)
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            Trace.TraceWarning(
                "[MainWindow] Call recording stream failed: {0} - {1}",
                e.Stream, e.Exception?.Message);
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
            // Unsubscribe from call recording events
            _callRecordingService.RecordingStarted -= OnCallRecordingStarted;
            _callRecordingService.RecordingStopped -= OnCallRecordingStopped;
            _callRecordingService.DurationUpdated -= OnCallRecordingDurationUpdated;
            _callRecordingService.StreamFailed -= OnCallRecordingStreamFailed;

            // Unsubscribe from read-aloud events
            _readAloudHotkeyService.ReadAloudStarted -= OnReadAloudStarted;
            _readAloudHotkeyService.ReadAloudPlaying -= OnReadAloudPlaying;
            _readAloudHotkeyService.ReadAloudCompleted -= OnReadAloudCompleted;
            _readAloudHotkeyService.ReadAloudCancelled -= OnReadAloudCancelled;

            // Clean up orchestrator, overlay, hotkey, and call recording services on actual exit
            _overlayWindow?.Close();
            _readAloudOverlayWindow?.Close();
            _orchestrator?.Dispose();
            _hotkeyService.Dispose();
            _callRecordingHotkeyService.Dispose();
            (_callRecordingService as IDisposable)?.Dispose();
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
                "Dictation" => new DictationPage(_settingsService, _audioCaptureService),
                "Templates" => new TemplatesPage(_templateService),
                "Recordings" => new TranscriptsPage(_transcriptStorageService),
                "Transcriptions" => new TranscribeFilesPage(_fileTranscriptionService),
                "TextToSpeech" => new TextToSpeechPage(
                    _textToSpeechService,
                    _highQualityRecorderService,
                    _highQualityLoopbackService,
                    _settingsService),
                "Settings" => new GeneralPage(_settingsService),
                "Models" => new AboutPage(_modelManager),
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
