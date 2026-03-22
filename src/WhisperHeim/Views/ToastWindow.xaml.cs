using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace WhisperHeim.Views;

/// <summary>
/// A small borderless toast that appears near the bottom-right of the primary screen,
/// fades in, stays for a few seconds, then fades out and closes itself.
/// </summary>
public partial class ToastWindow : Window
{
    private readonly DispatcherTimer _autoCloseTimer;

    public ToastWindow(string message, double displaySeconds = 3.0)
    {
        InitializeComponent();

        ToastText.Text = message;

        // Position near bottom-right of primary screen
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - 20;   // will adjust after measure
        Top = workArea.Bottom - 20;

        _autoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(displaySeconds)
        };
        _autoCloseTimer.Tick += (_, _) =>
        {
            _autoCloseTimer.Stop();
            FadeOutAndClose();
        };

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Measure actual size, then reposition
        UpdateLayout();
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - 12;
        Top = workArea.Bottom - ActualHeight - 12;

        // Fade in
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        fadeIn.Completed += (_, _) => _autoCloseTimer.Start();
        BeginAnimation(OpacityProperty, fadeIn);
    }

    private void FadeOutAndClose()
    {
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fadeOut);
    }

    /// <summary>
    /// Shows a toast on the UI thread. Safe to call from any thread.
    /// </summary>
    public static void Show(string message, double displaySeconds = 3.0)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;

        dispatcher.BeginInvoke(() =>
        {
            var toast = new ToastWindow(message, displaySeconds);
            toast.Show();
        });
    }
}
