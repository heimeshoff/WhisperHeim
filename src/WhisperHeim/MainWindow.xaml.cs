using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;

namespace WhisperHeim;

/// <summary>
/// Main settings window that lives in the system tray.
/// </summary>
public partial class MainWindow : FluentWindow
{
    private bool _isExiting;

    public MainWindow()
    {
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

    private void ShowWindow()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();
    }
}
