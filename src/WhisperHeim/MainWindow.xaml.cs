using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WhisperHeim.Services.Audio;
using WhisperHeim.Services.Models;
using WhisperHeim.Services.Settings;
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

    // Cache pages so they are not recreated on every navigation
    private readonly Dictionary<string, object> _pageCache = new();

    public MainWindow(SettingsService settingsService, IAudioCaptureService audioCaptureService, ModelManagerService modelManager)
    {
        _settingsService = settingsService;
        _audioCaptureService = audioCaptureService;
        _modelManager = modelManager;

        InitializeComponent();

        // Generate tray icon from Segoe Fluent Icons microphone glyph
        TrayIcon.Icon = CreateMicrophoneIcon();

        // Start minimized to tray - don't show the window
        Visibility = Visibility.Hidden;
        ShowInTaskbar = false;
    }

    /// <summary>
    /// Renders a microphone glyph from Segoe Fluent Icons into a BitmapSource
    /// suitable for use as a tray icon.
    /// </summary>
    private static ImageSource CreateMicrophoneIcon()
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
                Brushes.White,
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
                "Templates" => new TemplatesPage(_settingsService),
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
