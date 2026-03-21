using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WhisperHeim.Models;

namespace WhisperHeim.Views;

/// <summary>
/// A small, always-on-top, click-through overlay window that shows visual feedback
/// during read-aloud (text-to-speech via global hotkey). Uses the same positioning
/// and click-through behavior as the dictation overlay, but with a distinct purple
/// color and speaker icon.
///
/// Supports two visual states (see <see cref="ReadAloudOverlayState"/>):
///   Thinking -> purple, pulsing with spinning icon (model loading + audio generation)
///   Playing  -> purple, animated sound wave rings (active playback)
/// </summary>
public partial class ReadAloudOverlayWindow : Window
{
    // Win32 extended window styles for click-through and no-activate behavior
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    // Color animation duration for smooth transitions
    private static readonly Duration ColorTransitionDuration = new(TimeSpan.FromMilliseconds(300));

    // Purple color for read-aloud (distinct from dictation green)
    private static readonly Color PurpleColor = Color.FromRgb(0x9B, 0x59, 0xB6);

    private Storyboard? _thinkingPulse;
    private Storyboard? _playingPulse;
    private Storyboard? _fadeIn;
    private Storyboard? _fadeOut;

    private bool _isVisible;
    private ReadAloudOverlayState? _currentState;

    public ReadAloudOverlayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        // Make the window click-through and prevent it from stealing focus
        SetClickThrough();

        // Cache storyboard references
        _thinkingPulse = (Storyboard)FindResource("ThinkingPulse");
        _playingPulse = (Storyboard)FindResource("PlayingPulse");
        _fadeIn = (Storyboard)FindResource("FadeIn");
        _fadeOut = (Storyboard)FindResource("FadeOut");
    }

    /// <summary>
    /// Applies the overlay settings (size, position, opacity).
    /// Reuses the same OverlaySettings as the dictation overlay.
    /// </summary>
    public void ApplySettings(OverlaySettings settings)
    {
        Width = settings.Size;
        Height = settings.Size;

        // Scale the icon font size proportionally
        SpeakerIcon.FontSize = settings.Size * 0.42;

        // Apply max opacity (animations will modulate within this)
        MaxOpacity = settings.Opacity;

        PositionOnScreen(settings.Position);
    }

    /// <summary>
    /// The maximum opacity the overlay will reach (set from settings).
    /// </summary>
    private double MaxOpacity { get; set; } = 0.85;

    /// <summary>
    /// Shows the overlay with a fade-in animation and starts in Thinking state.
    /// </summary>
    public void ShowOverlay()
    {
        if (_isVisible) return;
        _isVisible = true;
        _currentState = null;

        Show();

        // Update the fade-in target to respect configured opacity
        if (_fadeIn != null)
        {
            var anim = (DoubleAnimation)_fadeIn.Children[0];
            anim.To = MaxOpacity;
        }

        _fadeIn?.Begin(this);

        // Default to Thinking state when first shown
        SetState(ReadAloudOverlayState.Thinking);

        Trace.TraceInformation("[ReadAloudOverlay] Shown.");
    }

    /// <summary>
    /// Hides the overlay with a fade-out animation.
    /// </summary>
    public void HideOverlay()
    {
        if (!_isVisible) return;
        _isVisible = false;
        _currentState = null;

        StopAllAnimations();

        if (_fadeOut != null)
        {
            _fadeOut.Completed -= OnFadeOutCompleted;
            _fadeOut.Completed += OnFadeOutCompleted;
            _fadeOut.Begin(this);
        }
        else
        {
            Hide();
        }

        Trace.TraceInformation("[ReadAloudOverlay] Hiding.");
    }

    /// <summary>
    /// Instantly hides the overlay without fade-out animation.
    /// Used when the user cancels with a second hotkey press.
    /// </summary>
    public void DismissOverlay()
    {
        if (!_isVisible) return;
        _isVisible = false;
        _currentState = null;

        StopAllAnimations();
        Hide();
        Opacity = 0;

        Trace.TraceInformation("[ReadAloudOverlay] Dismissed instantly.");
    }

    /// <summary>
    /// Sets the visual state of the overlay.
    /// Must be called on the UI thread.
    /// </summary>
    public void SetState(ReadAloudOverlayState newState)
    {
        if (!_isVisible) return;
        if (_currentState == newState) return;

        var previousState = _currentState;
        _currentState = newState;

        switch (newState)
        {
            case ReadAloudOverlayState.Thinking:
                _playingPulse?.Stop(this);
                ResetTransforms();
                // Use hourglass/loading icon during thinking
                SpeakerIcon.Text = "\uE916"; // U+E916 = Processing glyph
                _thinkingPulse?.Begin(this, true);
                break;

            case ReadAloudOverlayState.Playing:
                _thinkingPulse?.Stop(this);
                ResetTransforms();
                // Switch to speaker icon during playback
                SpeakerIcon.Text = "\uE767"; // U+E767 = Volume3 / speaker
                _playingPulse?.Begin(this, true);
                break;
        }

        Trace.TraceInformation("[ReadAloudOverlay] State: {0} -> {1}", previousState, newState);
    }

    private void StopAllAnimations()
    {
        _thinkingPulse?.Stop(this);
        _playingPulse?.Stop(this);
        ResetTransforms();
    }

    /// <summary>
    /// Resets all transforms to their default values.
    /// </summary>
    private void ResetTransforms()
    {
        ScaleTransform.ScaleX = 1.0;
        ScaleTransform.ScaleY = 1.0;
        OverlayEllipse.Opacity = 1.0;
        IconRotateTransform.Angle = 0;
        WaveRing1.Opacity = 0;
        WaveRing2.Opacity = 0;
        WaveRingScale1.ScaleX = 1.0;
        WaveRingScale1.ScaleY = 1.0;
        WaveRingScale2.ScaleX = 1.0;
        WaveRingScale2.ScaleY = 1.0;
    }

    private void OnFadeOutCompleted(object? sender, EventArgs e)
    {
        _fadeOut!.Completed -= OnFadeOutCompleted;
        Hide();
        Opacity = 0;
    }

    /// <summary>
    /// Sets the WS_EX_TRANSPARENT, WS_EX_NOACTIVATE, and WS_EX_TOOLWINDOW
    /// extended styles so the window does not steal focus, does not appear
    /// in Alt+Tab, and passes all mouse input through to windows behind it.
    /// </summary>
    private void SetClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }

    /// <summary>
    /// Positions the overlay on the primary screen based on a named position.
    /// Same positioning logic as the dictation overlay.
    /// </summary>
    private void PositionOnScreen(string position)
    {
        var workArea = SystemParameters.WorkArea;
        const double margin = 20;

        switch (position)
        {
            case "TopLeft":
                Left = workArea.Left + margin;
                Top = workArea.Top + margin;
                break;
            case "TopCenter":
                Left = workArea.Left + (workArea.Width - Width) / 2;
                Top = workArea.Top + margin;
                break;
            case "TopRight":
                Left = workArea.Right - Width - margin;
                Top = workArea.Top + margin;
                break;
            case "BottomLeft":
                Left = workArea.Left + margin;
                Top = workArea.Bottom - Height - margin;
                break;
            case "BottomRight":
                Left = workArea.Right - Width - margin;
                Top = workArea.Bottom - Height - margin;
                break;
            case "BottomCenter":
            default:
                Left = workArea.Left + (workArea.Width - Width) / 2;
                Top = workArea.Bottom - Height - margin;
                break;
        }
    }
}
