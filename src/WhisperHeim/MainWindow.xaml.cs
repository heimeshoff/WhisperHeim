using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using WhisperHeim.Services.Audio;
using WhisperHeim.Services.CallTranscription;
using WhisperHeim.Services.Dictation;
using WhisperHeim.Services.FileTranscription;
using WhisperHeim.Services.Recording;
using WhisperHeim.Services.Transcription;
using WhisperHeim.Views.Controls;
using WhisperHeim.Services.Hotkey;
using WhisperHeim.Services.Input;
using WhisperHeim.Services.Models;
using WhisperHeim.Services.Orchestration;
using WhisperHeim.Services.Settings;
using WhisperHeim.Services.Templates;
using WhisperHeim.Services.SelectedText;
using WhisperHeim.Services.TextToSpeech;
using WhisperHeim.Converters;
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

    // File transcription for imported audio files on the Recordings page
    private readonly IFileTranscriptionService _fileTranscriptionService;

    // High-quality loopback for voice cloning from system audio
    private readonly IHighQualityLoopbackService _highQualityLoopbackService;

    // High-quality mic recorder for voice cloning
    private readonly IHighQualityRecorderService _highQualityRecorderService;

    // Text-to-speech for the TTS page
    private readonly ITextToSpeechService _textToSpeechService;

    // Data path service for resolving user-configured data directory
    private readonly DataPathService _dataPathService;

    // Read-aloud hotkey service (captures selected text and signals navigation)
    private readonly ReadAloudHotkeyService _readAloudHotkeyService;

    // Transcription queue — replaces the old TranscriptionBusyService
    private readonly TranscriptionQueueService _transcriptionQueueService;

    // Hotkey and orchestration
    private readonly GlobalHotkeyService _hotkeyService = new();
    private DictationOrchestrator? _orchestrator;

    // Tray icon images
    private ImageSource? _idleIcon;
    private ImageSource? _recordingIcon;
    private ImageSource? _callRecordingIcon;

    // Dictation overlay indicator
    private DictationOverlayWindow? _overlayWindow;

    // Cache pages so they are not recreated on every navigation
    private readonly Dictionary<string, object> _pageCache = new();

    // (Queue is now managed by TranscriptionQueueService)

    // Sidebar collapsed state
    private bool _isSidebarCollapsed;
    private const double SidebarExpandedWidth = 200;
    private const double SidebarCollapsedWidth = 64;

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
        DataPathService dataPathService,
        ReadAloudHotkeyService readAloudHotkeyService,
        TranscriptionQueueService transcriptionQueueService)
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
        _dataPathService = dataPathService;
        _readAloudHotkeyService = readAloudHotkeyService;
        _transcriptionQueueService = transcriptionQueueService;

        InitializeComponent();

        // Wire up the transcription queue bottom bar
        TranscriptionBar.Initialize(_transcriptionQueueService);
        _transcriptionQueueService.ItemCompleted += OnTranscriptionItemCompleted;
        _transcriptionQueueService.ItemFailed += OnTranscriptionItemFailed;

        // Restore saved window position/size or center on screen
        RestoreWindowPosition();

        // Restore sidebar collapsed state from settings
        if (_settingsService.Current.Window.SidebarCollapsed)
        {
            ApplySidebarCollapsedState(collapsed: true, animate: false);
        }

        // Load the initial page now that InitializeComponent has set up PageContent
        NavigateTo("Dictation");

        // Generate tray icons for idle, dictation, and call recording states
        _idleIcon = CreateTwoToneTrayIcon();
        _recordingIcon = CreateMicrophoneIcon(new SolidColorBrush(Color.FromRgb(0x44, 0xCC, 0x44)));
        _callRecordingIcon = CreateMicrophoneIcon(Brushes.Orange);
        TrayIcon.Icon = _idleIcon;

        // Set the window/taskbar icon to the two-tone logo
        Icon = CreateTwoToneLogoIcon();

        // Start minimized to tray - don't show the window
        Visibility = Visibility.Hidden;
        ShowInTaskbar = false;

        // Low-level keyboard hooks don't need an HWND, so we can set up immediately
        SetupHotkeysAndOrchestration();
    }

    /// <summary>
    /// Shows the window off-screen so the visual tree renders (registering the tray
    /// icon), then immediately hides it. Called from App.xaml.cs when starting minimized.
    /// </summary>
    public void InitializeTrayAndHide()
    {
        // Remember the real position/size set by RestoreWindowPosition() in the constructor.
        var savedLeft = Left;
        var savedTop = Top;
        var savedWidth = Width;
        var savedHeight = Height;

        // Create the Win32 HWND without showing the window at all.
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        helper.EnsureHandle();

        // Move the window off-screen and make it zero-size at the Win32 level
        // BEFORE any WPF render pass can display it.
        SetWindowPos(helper.Handle, IntPtr.Zero, -32000, -32000, 0, 0,
            SWP_NOZORDER | SWP_NOACTIVATE);

        // Show (triggers visual tree load / tray icon registration) then hide.
        // The window is zero-size and off-screen so nothing is visible.
        ShowActivated = false;
        ShowInTaskbar = false;
        Show();
        Hide();

        // Restore the real position/size so the next ShowWindow() opens correctly.
        Left = savedLeft;
        Top = savedTop;
        Width = savedWidth;
        Height = savedHeight;
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

        // Wire up the hold-to-talk orchestrator (with template support via Alt modifier)
        _orchestrator = new DictationOrchestrator(
            _hotkeyService,
            _audioCaptureService,
            _transcriptionService,
            _inputSimulator,
            OnDictationStateChanged,
            _templateService);

        // Wire up audio amplitude for overlay RMS visualization
        _orchestrator.AudioAmplitudeChanged += OnAudioAmplitudeChanged;

        // Wire up pipeline errors for overlay error state
        _orchestrator.PipelineError += OnPipelineError;

        // Show a toast when template mode doesn't find a match
        _orchestrator.TemplateNoMatch += spokenText =>
            Views.ToastWindow.Show($"No template match for: \"{spokenText}\"");

        _orchestrator.Start();

        // Initialize the dictation overlay if enabled in settings
        InitializeOverlay();

        // Wire up read-aloud hotkey: captures text and navigates to TTS page
        _readAloudHotkeyService.TextCaptured += OnReadAloudTextCaptured;

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

        // Eagerly create the Transcripts page so it receives recording events
        // (start/stop) even before the user navigates to it.
        GetOrCreateTranscriptsPage();
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

        Trace.TraceInformation("[MainWindow] Overlay initialized (pill mode, follows last click).");
    }

    /// <summary>
    /// Handles the read-aloud hotkey: brings window to foreground, navigates to TTS page,
    /// and pastes the captured text into the input workspace.
    /// </summary>
    private void OnReadAloudTextCaptured(object? sender, ReadAloudTextCapturedEventArgs e)
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            // Bring the window to the foreground, restoring from minimized if needed
            ShowWindow();

            // Navigate to the Text to Speech page
            NavigateTo("TextToSpeech");

            // Select the TTS nav item to keep sidebar in sync
            foreach (var item in NavList.Items.OfType<System.Windows.Controls.ListBoxItem>())
            {
                if (item.Tag is string tag && tag == "TextToSpeech")
                {
                    NavList.SelectedItem = item;
                    break;
                }
            }

            // Set the captured text into the TTS input workspace
            if (_pageCache.TryGetValue("TextToSpeech", out var page) && page is TextToSpeechPage ttsPage)
            {
                ttsPage.SetInputText(e.Text);
            }

            Trace.TraceInformation("[MainWindow] Read-aloud: navigated to TTS page with {0} chars of text.", e.Text.Length);
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
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            if (_overlayWindow is null) return;

            // Stay in Speaking state the entire time dictation is active;
            // bars animate based on amplitude (small when quiet, large when loud)
            _overlayWindow.SetMicState(Views.OverlayMicState.Speaking);
            _overlayWindow.UpdateAmplitude(rmsAmplitude);
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

            if (e.Exception is not null)
            {
                Trace.TraceWarning(
                    "[MainWindow] Call recording stopped with error: {0}",
                    e.Exception.Message);
            }

            // Auto-transcription is handled by TranscriptsPage via its recording
            // service subscription. Just ensure the page cache is warm so the
            // event handler is wired up.
            GetOrCreateTranscriptsPage();
        });
    }

    private void EnqueueTranscription(CallRecordingSession session)
    {
        // Derive a title from the session directory name (e.g. "2026-03-25_14-30-00")
        var sessionDir = Path.GetDirectoryName(session.MicWavFilePath);
        var title = Path.GetFileName(sessionDir) ?? "Recording";
        _transcriptionQueueService.Enqueue(title, session);
    }

    private void OnTranscriptionItemCompleted(object? sender, TranscriptionQueueItem item)
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            var transcriptsPage = GetOrCreateTranscriptsPage();
            transcriptsPage.RefreshList();
        });
    }

    private void OnTranscriptionItemFailed(object? sender, TranscriptionQueueItem item)
    {
        // Refresh pending list so cancelled/failed recordings move back to pending
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            var transcriptsPage = GetOrCreateTranscriptsPage();
            transcriptsPage.RefreshList();
        });
    }

    private TranscriptsPage GetOrCreateTranscriptsPage()
    {
        if (_pageCache.TryGetValue("Recordings", out var cached) && cached is TranscriptsPage page)
            return page;

        page = new TranscriptsPage(_transcriptStorageService, _transcriptionQueueService, _callRecordingService, _fileTranscriptionService);
        page.TranscriptionRequested += OnPendingTranscriptionRequested;
        page.ReTranscriptionRequested += OnPendingTranscriptionRequested;
        _pageCache["Recordings"] = page;
        return page;
    }

    private void OnPendingTranscriptionRequested(object? sender, CallRecordingSession session)
    {
        EnqueueTranscription(session);
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
    /// Creates a two-tone (blue head + orange stand) tray icon using the custom mic paths.
    /// </summary>
    private static ImageSource CreateTwoToneTrayIcon()
    {
        const int size = 32;
        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            // Mic paths actual bounds: x=[6,18] y=[2,22.5] → width=12, height=20.5
            const double pathH = 20.5;
            const double pathX = 6.0;
            const double pathY = 2.0;
            const double pathW = 12.0;
            double scale = (size - 4) / pathH; // small padding for tray
            double offsetX = (size - pathW * scale) / 2 - pathX * scale;
            double offsetY = (size - pathH * scale) / 2 - pathY * scale;

            var blueBrush = new SolidColorBrush(Color.FromRgb(0x25, 0xab, 0xfe));
            var orangeBrush = new SolidColorBrush(Color.FromRgb(0xff, 0x8b, 0x00));

            var headGeometry = Geometry.Parse("M12,2 C9.79,2 8,3.79 8,6 L8,12 C8,14.21 9.79,16 12,16 C14.21,16 16,14.21 16,12 L16,6 C16,3.79 14.21,2 12,2 Z");
            var standGeometry = Geometry.Parse("M6,11 L6,12 C6,15.31 8.69,18 12,18 C15.31,18 18,15.31 18,12 L18,11 L16.5,11 L16.5,12 C16.5,14.49 14.49,16.5 12,16.5 C9.51,16.5 7.5,14.49 7.5,12 L7.5,11 Z M11.25,18.5 L11.25,21 L8.5,21 L8.5,22.5 L15.5,22.5 L15.5,21 L12.75,21 L12.75,18.5 Z");

            ctx.PushTransform(new TranslateTransform(offsetX, offsetY));
            ctx.PushTransform(new ScaleTransform(scale, scale));
            ctx.DrawGeometry(blueBrush, null, headGeometry);
            ctx.DrawGeometry(orangeBrush, null, standGeometry);
            ctx.Pop();
            ctx.Pop();
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// Creates the two-tone microphone logo for the window/taskbar icon.
    /// Blue capsule head + orange stand, no background box.
    /// </summary>
    private static ImageSource CreateTwoToneLogoIcon()
    {
        const int size = 32;
        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            // Mic paths actual bounds: x=[6,18] y=[2,22.5] → width=12, height=20.5
            // Scale to fill the full 32x32 icon (fit height, center horizontally)
            const double pathW = 12.0;
            const double pathH = 20.5;
            const double pathX = 6.0;
            const double pathY = 2.0;
            double scale = size / pathH;
            double offsetX = (size - pathW * scale) / 2 - pathX * scale;
            double offsetY = -pathY * scale;

            var blueBrush = new SolidColorBrush(Color.FromRgb(0x25, 0xab, 0xfe));
            var orangeBrush = new SolidColorBrush(Color.FromRgb(0xff, 0x8b, 0x00));

            // Microphone head (capsule) - Blue
            var headGeometry = Geometry.Parse("M12,2 C9.79,2 8,3.79 8,6 L8,12 C8,14.21 9.79,16 12,16 C14.21,16 16,14.21 16,12 L16,6 C16,3.79 14.21,2 12,2 Z");
            // Microphone stand (arc + stem + base) - Orange
            var standGeometry = Geometry.Parse("M6,11 L6,12 C6,15.31 8.69,18 12,18 C15.31,18 18,15.31 18,12 L18,11 L16.5,11 L16.5,12 C16.5,14.49 14.49,16.5 12,16.5 C9.51,16.5 7.5,14.49 7.5,12 L7.5,11 Z M11.25,18.5 L11.25,21 L8.5,21 L8.5,22.5 L15.5,22.5 L15.5,21 L12.75,21 L12.75,18.5 Z");

            ctx.PushTransform(new TranslateTransform(offsetX, offsetY));
            ctx.PushTransform(new ScaleTransform(scale, scale));
            ctx.DrawGeometry(blueBrush, null, headGeometry);
            ctx.DrawGeometry(orangeBrush, null, standGeometry);
            ctx.Pop();
            ctx.Pop();
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
        // Save window position/size whenever the window is closed or hidden
        SaveWindowPosition();

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
            _readAloudHotkeyService.TextCaptured -= OnReadAloudTextCaptured;

            // Clean up orchestrator, overlay, hotkey, and call recording services on actual exit
            _overlayWindow?.Close();
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
        Visibility = Visibility.Visible;
        Show();
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
    }

    private bool _suppressNavSelectionSync;

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Guard: SelectionChanged fires during InitializeComponent before controls are ready
        if (PageContent is null) return;

        if (_suppressNavSelectionSync) return;

        if (NavList.SelectedItem is ListBoxItem item && item.Tag is string tag)
        {
            // Clear bottom list selection when main list is selected
            _suppressNavSelectionSync = true;
            NavBottomList.SelectedIndex = -1;
            _suppressNavSelectionSync = false;

            NavigateTo(tag);
        }
    }

    private void NavBottomList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PageContent is null) return;

        if (_suppressNavSelectionSync) return;

        if (NavBottomList.SelectedItem is ListBoxItem item && item.Tag is string tag)
        {
            // Clear main list selection when bottom list is selected
            _suppressNavSelectionSync = true;
            NavList.SelectedIndex = -1;
            _suppressNavSelectionSync = false;

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
                "Recordings" => GetOrCreateTranscriptsPage(),
                "TextToSpeech" => new TextToSpeechPage(
                    _textToSpeechService,
                    _highQualityRecorderService,
                    _highQualityLoopbackService,
                    _settingsService,
                    _dataPathService),
                "Settings" => new GeneralPage(_settingsService),
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

    // ── Sidebar collapse/expand ────────────────────────────────────────

    private void SidebarToggle_Click(object sender, RoutedEventArgs e)
    {
        ApplySidebarCollapsedState(!_isSidebarCollapsed, animate: true);

        // Persist the state
        _settingsService.Current.Window.SidebarCollapsed = _isSidebarCollapsed;
        _settingsService.Save();
    }

    /// <summary>
    /// Applies the sidebar collapsed or expanded state, optionally with animation.
    /// </summary>
    private void ApplySidebarCollapsedState(bool collapsed, bool animate)
    {
        _isSidebarCollapsed = collapsed;

        var targetWidth = collapsed ? SidebarCollapsedWidth : SidebarExpandedWidth;
        var labelVisibility = collapsed ? Visibility.Collapsed : Visibility.Visible;

        // Update toggle chevron appearance
        SidebarToggleIcon.Symbol = collapsed
            ? Wpf.Ui.Controls.SymbolRegular.ChevronRight24
            : Wpf.Ui.Controls.SymbolRegular.ChevronLeft24;
        SidebarToggleButton.ToolTip = collapsed ? "Expand sidebar" : "Collapse sidebar";

        // Show/hide text labels and adjust branding layout
        BrandingTitle.Visibility = labelVisibility;
        BrandingLogo.Margin = collapsed ? new Thickness(0) : new Thickness(0, 0, 10, 0);
        BrandingHeader.Margin = collapsed ? new Thickness(4, 12, 4, 24) : new Thickness(16, 12, 16, 24);
        BrandingHeader.HorizontalAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        NavLabelDictation.Visibility = labelVisibility;
        NavLabelTemplates.Visibility = labelVisibility;
        NavLabelRecordings.Visibility = labelVisibility;
        NavLabelTextToSpeech.Visibility = labelVisibility;
        NavLabelSettings.Visibility = labelVisibility;
        NavLabelAbout.Visibility = labelVisibility;

        // Adjust icon margins when collapsed (center the icons)
        var iconMargin = collapsed ? new Thickness(0) : new Thickness(0, 0, 10, 0);
        foreach (var item in NavList.Items.OfType<ListBoxItem>().Concat(NavBottomList.Items.OfType<ListBoxItem>()))
        {
            if (item.Content is StackPanel sp && sp.Children[0] is Wpf.Ui.Controls.SymbolIcon icon)
            {
                icon.Margin = iconMargin;
            }
        }

        // Adjust nav panel margin for collapsed mode (center content)
        NavPanel.Margin = collapsed
            ? new Thickness(4, 0, 4, 12)
            : new Thickness(12, 0, 4, 12);

        if (animate)
        {
            var animation = new GridLengthAnimation
            {
                From = new GridLength(collapsed ? SidebarExpandedWidth : SidebarCollapsedWidth),
                To = new GridLength(targetWidth),
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, animation);
        }
        else
        {
            SidebarColumn.Width = new GridLength(targetWidth);
        }
    }

    // ── Window position persistence ─────────────────────────────────────

    private const double DefaultWidth = 1200;
    private const double DefaultHeight = 800;

    /// <summary>
    /// Restores saved window position/size from settings, or centers on screen at default size.
    /// If saved position is off-screen (e.g., monitor disconnected), falls back to centered.
    /// </summary>
    private void RestoreWindowPosition()
    {
        var ws = _settingsService.Current.Window;

        if (ws.Left.HasValue && ws.Top.HasValue && ws.Width.HasValue && ws.Height.HasValue)
        {
            var savedRect = new System.Windows.Rect(ws.Left.Value, ws.Top.Value, ws.Width.Value, ws.Height.Value);

            if (IsRectOnScreen(savedRect))
            {
                Left = ws.Left.Value;
                Top = ws.Top.Value;
                Width = ws.Width.Value;
                Height = ws.Height.Value;

                if (ws.IsMaximized)
                {
                    WindowState = WindowState.Maximized;
                }

                Trace.TraceInformation("[MainWindow] Restored window position: {0},{1} {2}x{3} maximized={4}",
                    ws.Left, ws.Top, ws.Width, ws.Height, ws.IsMaximized);
                return;
            }

            Trace.TraceInformation("[MainWindow] Saved window position is off-screen, centering on primary monitor.");
        }

        // First launch or off-screen: center on primary screen at default size
        CenterOnPrimaryScreen();
    }

    /// <summary>
    /// Centers the window on the primary screen's work area.
    /// </summary>
    private void CenterOnPrimaryScreen()
    {
        var workArea = SystemParameters.WorkArea;
        Width = DefaultWidth;
        Height = DefaultHeight;
        Left = workArea.Left + (workArea.Width - DefaultWidth) / 2;
        Top = workArea.Top + (workArea.Height - DefaultHeight) / 2;
    }

    /// <summary>
    /// Checks if at least 100x100 pixels of the given rectangle are visible on any monitor.
    /// Uses Win32 EnumDisplayMonitors to enumerate all connected monitors.
    /// </summary>
    private static bool IsRectOnScreen(System.Windows.Rect rect)
    {
        const double minVisiblePixels = 100;
        bool isOnScreen = false;

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
        {
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                var workArea = new System.Windows.Rect(
                    monitorInfo.rcWork.left,
                    monitorInfo.rcWork.top,
                    monitorInfo.rcWork.right - monitorInfo.rcWork.left,
                    monitorInfo.rcWork.bottom - monitorInfo.rcWork.top);

                var intersection = System.Windows.Rect.Intersect(rect, workArea);
                if (!intersection.IsEmpty &&
                    intersection.Width >= minVisiblePixels &&
                    intersection.Height >= minVisiblePixels)
                {
                    isOnScreen = true;
                }
            }
            return true; // continue enumeration
        }, IntPtr.Zero);

        return isOnScreen;
    }

    // ── Win32 interop for off-screen window positioning ─────────────────

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    // ── Win32 interop for multi-monitor detection ───────────────────────

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    /// <summary>
    /// Saves the current window position, size, and maximized state to settings.
    /// </summary>
    private void SaveWindowPosition()
    {
        var ws = _settingsService.Current.Window;
        ws.IsMaximized = WindowState == WindowState.Maximized;

        // Save the restore bounds (normal position) even when maximized,
        // so we can restore to the correct position when un-maximizing.
        var bounds = WindowState == WindowState.Maximized ? RestoreBounds : new System.Windows.Rect(Left, Top, Width, Height);

        ws.Left = bounds.Left;
        ws.Top = bounds.Top;
        ws.Width = bounds.Width;
        ws.Height = bounds.Height;

        _settingsService.Save();

        Trace.TraceInformation("[MainWindow] Saved window position: {0},{1} {2}x{3} maximized={4}",
            ws.Left, ws.Top, ws.Width, ws.Height, ws.IsMaximized);
    }
}
