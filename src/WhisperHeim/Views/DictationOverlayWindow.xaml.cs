using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WhisperHeim.Models;

namespace WhisperHeim.Views;

/// <summary>
/// A small, always-on-top, click-through overlay window that shows an animated
/// microphone indicator during active dictation. Uses WS_EX_TRANSPARENT and
/// WS_EX_NOACTIVATE to avoid stealing focus or blocking mouse clicks.
///
/// Supports four visual states (see <see cref="OverlayMicState"/>):
///   Idle     -> green, gentle breathing pulse
///   Speaking -> green, RMS amplitude-driven ring scaling
///   NoMic    -> grey, static
///   Error    -> red, static
/// </summary>
public partial class DictationOverlayWindow : Window
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

    // State colors
    private static readonly Color GreenColor = Color.FromRgb(0x44, 0xCC, 0x44);
    private static readonly Color GreyColor = Color.FromRgb(0x99, 0x99, 0x99);
    private static readonly Color RedColor = Color.FromRgb(0xEE, 0x33, 0x33);

    // Color animation duration for smooth transitions
    private static readonly Duration ColorTransitionDuration = new(TimeSpan.FromMilliseconds(300));

    private Storyboard? _listeningPulse;
    private Storyboard? _speechPulse;
    private Storyboard? _fadeIn;
    private Storyboard? _fadeOut;

    private bool _isVisible;
    private OverlayMicState _currentState = OverlayMicState.Idle;

    // Smoothed RMS value for amplitude-driven animation (exponential moving average)
    private double _smoothedRms;
    private const double RmsSmoothingFactor = 0.3; // 0..1, higher = more responsive

    public DictationOverlayWindow()
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
        _listeningPulse = (Storyboard)FindResource("ListeningPulse");
        _speechPulse = (Storyboard)FindResource("SpeechPulse");
        _fadeIn = (Storyboard)FindResource("FadeIn");
        _fadeOut = (Storyboard)FindResource("FadeOut");
    }

    /// <summary>
    /// Applies the overlay settings (size, position, opacity).
    /// </summary>
    public void ApplySettings(OverlaySettings settings)
    {
        Width = settings.Size;
        Height = settings.Size;

        // Scale the icon font size proportionally
        MicIcon.FontSize = settings.Size * 0.42;

        // Apply max opacity (animations will modulate within this)
        MaxOpacity = settings.Opacity;

        PositionOnScreen(settings.Position);
    }

    /// <summary>
    /// The maximum opacity the overlay will reach (set from settings).
    /// </summary>
    private double MaxOpacity { get; set; } = 0.85;

    /// <summary>
    /// Shows the overlay with a fade-in animation and starts the listening pulse.
    /// </summary>
    public void ShowOverlay()
    {
        if (_isVisible) return;
        _isVisible = true;

        Show();

        // Update the fade-in target to respect configured opacity
        if (_fadeIn != null)
        {
            var anim = (DoubleAnimation)_fadeIn.Children[0];
            anim.To = MaxOpacity;
        }

        _fadeIn?.Begin(this);

        // Default to Idle state when first shown
        SetMicState(OverlayMicState.Idle);

        Trace.TraceInformation("[DictationOverlay] Shown.");
    }

    /// <summary>
    /// Hides the overlay with a fade-out animation.
    /// </summary>
    public void HideOverlay()
    {
        if (!_isVisible) return;
        _isVisible = false;
        _currentState = OverlayMicState.Idle;

        _listeningPulse?.Stop(this);
        _speechPulse?.Stop(this);

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

        Trace.TraceInformation("[DictationOverlay] Hiding.");
    }

    /// <summary>
    /// Sets the visual state of the overlay microphone indicator.
    /// Must be called on the UI thread.
    /// </summary>
    public void SetMicState(OverlayMicState newState)
    {
        if (!_isVisible) return;
        if (_currentState == newState) return;

        var previousState = _currentState;
        _currentState = newState;

        // Determine target color
        var targetColor = newState switch
        {
            OverlayMicState.Idle => GreenColor,
            OverlayMicState.Speaking => GreenColor,
            OverlayMicState.NoMic => GreyColor,
            OverlayMicState.Error => RedColor,
            _ => GreenColor
        };

        // Animate color transition for smooth visual change
        AnimateColor(targetColor);

        // Manage pulse animations based on state
        switch (newState)
        {
            case OverlayMicState.Idle:
                _speechPulse?.Stop(this);
                _smoothedRms = 0;
                ResetScaleTransform();
                _listeningPulse?.Begin(this, true);
                break;

            case OverlayMicState.Speaking:
                // Stop the listening pulse; RMS-driven scaling is applied directly
                _listeningPulse?.Stop(this);
                _speechPulse?.Stop(this);
                _smoothedRms = 0;
                ResetScaleTransform();
                break;

            case OverlayMicState.NoMic:
            case OverlayMicState.Error:
                _listeningPulse?.Stop(this);
                _speechPulse?.Stop(this);
                _smoothedRms = 0;
                ResetScaleTransform();
                break;
        }

        Trace.TraceInformation("[DictationOverlay] State: {0} -> {1}", previousState, newState);
    }

    /// <summary>
    /// Updates the overlay ring scale based on real-time RMS audio amplitude.
    /// Must be called on the UI thread. Only has effect in <see cref="OverlayMicState.Speaking"/> state.
    /// </summary>
    /// <param name="rmsAmplitude">RMS amplitude in [0.0, 1.0] range.</param>
    public void UpdateAmplitude(double rmsAmplitude)
    {
        if (!_isVisible || _currentState != OverlayMicState.Speaking) return;

        // Clamp input
        rmsAmplitude = Math.Clamp(rmsAmplitude, 0.0, 1.0);

        // Smooth with exponential moving average to avoid jitter
        _smoothedRms = _smoothedRms + RmsSmoothingFactor * (rmsAmplitude - _smoothedRms);

        // Map smoothed RMS to scale range: 0.92 (silent) to 1.12 (loud)
        // Use a slight curve for better visual response
        var normalized = Math.Min(_smoothedRms * 3.0, 1.0); // amplify low values
        var scale = 0.92 + normalized * 0.20;

        ScaleTransform.ScaleX = scale;
        ScaleTransform.ScaleY = scale;

        // Also modulate opacity slightly for visual feedback
        OverlayEllipse.Opacity = 0.7 + normalized * 0.3;
    }

    /// <summary>
    /// Convenience: Switches to the faster speech animation (call when VAD detects speech).
    /// </summary>
    public void NotifySpeechActivity()
    {
        SetMicState(OverlayMicState.Speaking);
    }

    /// <summary>
    /// Convenience: Switches back to the gentle listening animation (call when speech pauses).
    /// </summary>
    public void NotifySpeechPause()
    {
        SetMicState(OverlayMicState.Idle);
    }

    /// <summary>
    /// Animates both the ellipse stroke and mic icon foreground to the target color.
    /// </summary>
    private void AnimateColor(Color targetColor)
    {
        var strokeAnim = new ColorAnimation(targetColor, ColorTransitionDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };

        var iconAnim = new ColorAnimation(targetColor, ColorTransitionDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };

        EllipseStrokeBrush.BeginAnimation(SolidColorBrush.ColorProperty, strokeAnim);
        MicIconBrush.BeginAnimation(SolidColorBrush.ColorProperty, iconAnim);
    }

    /// <summary>
    /// Resets the scale transform to 1.0 (no scaling).
    /// </summary>
    private void ResetScaleTransform()
    {
        ScaleTransform.ScaleX = 1.0;
        ScaleTransform.ScaleY = 1.0;
        OverlayEllipse.Opacity = 1.0;
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
