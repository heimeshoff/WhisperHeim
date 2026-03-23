using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WhisperHeim.Models;

namespace WhisperHeim.Views;

/// <summary>
/// A pill-shaped, always-on-top, click-through overlay window that shows animated
/// frequency bars during active dictation. Appears at the last globally-clicked
/// mouse position. Uses WS_EX_TRANSPARENT and WS_EX_NOACTIVATE to avoid stealing
/// focus or blocking mouse clicks.
///
/// Supports four visual states (see <see cref="OverlayMicState"/>):
///   Idle     -> blue border, orange bars with gentle movement
///   Speaking -> blue border, orange bars driven by RMS amplitude
///   NoMic    -> grey border, grey static bars
///   Error    -> solid red fill
/// </summary>
public partial class DictationOverlayWindow : Window
{
    // ── Win32 constants ──────────────────────────────────────────────────
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    // Global mouse hook constants
    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;

    // ── P/Invoke declarations ────────────────────────────────────────────
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    // ── Brand colors ─────────────────────────────────────────────────────
    private static readonly Color BlueBorderColor = (Color)ColorConverter.ConvertFromString("#FF25abfe");
    private static readonly Color OrangeBarColor = (Color)ColorConverter.ConvertFromString("#FFff8b00");
    private static readonly Color GreyColor = Color.FromRgb(0x99, 0x99, 0x99);
    private static readonly Color RedColor = Color.FromRgb(0xEE, 0x33, 0x33);

    private static readonly Duration ColorTransitionDuration = new(TimeSpan.FromMilliseconds(300));

    // ── Bar configuration ────────────────────────────────────────────────
    private const int BarCount = 18;
    private const double BarGap = 2.0;
    private const double MinBarHeightFraction = 0.15; // minimum bar height as fraction of canvas height

    private readonly Rectangle[] _bars = new Rectangle[BarCount];
    private readonly Random _random = new();

    // ── Animation state ──────────────────────────────────────────────────
    private Storyboard? _fadeIn;
    private Storyboard? _fadeOut;
    private DispatcherTimer? _barAnimationTimer;

    private bool _isVisible;
    private OverlayMicState _currentState = OverlayMicState.Idle;

    // Smoothed RMS value for amplitude-driven animation
    private double _smoothedRms;
    private const double RmsSmoothingFactor = 0.3;

    // ── Global mouse hook state ──────────────────────────────────────────
    private IntPtr _mouseHookHandle = IntPtr.Zero;
    private LowLevelMouseProc? _mouseHookProc; // prevent GC of delegate
    private double _lastClickX;
    private double _lastClickY;

    /// <summary>
    /// The maximum opacity the overlay will reach (set from settings).
    /// </summary>
    private double MaxOpacity { get; set; } = 0.85;

    public DictationOverlayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;

        // Set initial click position to center-bottom of primary screen
        var workArea = SystemParameters.WorkArea;
        _lastClickX = workArea.Left + workArea.Width / 2;
        _lastClickY = workArea.Bottom - 60;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        SetClickThrough();
        InitializeBars();
        InstallGlobalMouseHook();

        _fadeIn = (Storyboard)FindResource("FadeIn");
        _fadeOut = (Storyboard)FindResource("FadeOut");

        // Timer for animating bars (~30 fps)
        _barAnimationTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _barAnimationTimer.Tick += OnBarAnimationTick;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _barAnimationTimer?.Stop();
        UninstallGlobalMouseHook();
    }

    // ── Bar initialization ───────────────────────────────────────────────

    private void InitializeBars()
    {
        BarsCanvas.Children.Clear();
        for (int i = 0; i < BarCount; i++)
        {
            var bar = new Rectangle
            {
                Fill = new SolidColorBrush(OrangeBarColor),
                RadiusX = 1.5,
                RadiusY = 1.5
            };
            _bars[i] = bar;
            BarsCanvas.Children.Add(bar);
        }

        // Layout bars when canvas size is known
        BarsCanvas.SizeChanged += OnCanvasSizeChanged;
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        LayoutBars();
    }

    private void LayoutBars()
    {
        double canvasWidth = BarsCanvas.ActualWidth;
        double canvasHeight = BarsCanvas.ActualHeight;
        if (canvasWidth <= 0 || canvasHeight <= 0) return;

        double totalGaps = (BarCount - 1) * BarGap;
        double barWidth = (canvasWidth - totalGaps) / BarCount;
        if (barWidth < 1) barWidth = 1;

        for (int i = 0; i < BarCount; i++)
        {
            var bar = _bars[i];
            double x = i * (barWidth + BarGap);
            double barHeight = canvasHeight * MinBarHeightFraction;

            bar.Width = barWidth;
            bar.Height = barHeight;
            Canvas.SetLeft(bar, x);
            Canvas.SetTop(bar, canvasHeight - barHeight);
        }
    }

    // ── Bar animation tick ───────────────────────────────────────────────

    private void OnBarAnimationTick(object? sender, EventArgs e)
    {
        double canvasHeight = BarsCanvas.ActualHeight;
        if (canvasHeight <= 0) return;

        for (int i = 0; i < BarCount; i++)
        {
            double targetHeight;

            if (_currentState == OverlayMicState.Speaking || _currentState == OverlayMicState.Idle)
            {
                // Use smoothed RMS; for Idle, _smoothedRms will be ~0 giving minimal movement
                double amplitude = _currentState == OverlayMicState.Idle
                    ? 0.05 // gentle idle movement
                    : Math.Max(_smoothedRms, 0.05);

                // Amplify so even quiet speech shows visible bars
                double normalized = Math.Min(amplitude * 3.0, 1.0);

                // Per-bar random variation for spectrum analyzer effect
                double randomFactor = 0.4 + _random.NextDouble() * 0.6;
                double heightFraction = MinBarHeightFraction + (1.0 - MinBarHeightFraction) * normalized * randomFactor;

                targetHeight = canvasHeight * heightFraction;
            }
            else
            {
                // NoMic or Error: static minimal bars
                targetHeight = canvasHeight * MinBarHeightFraction;
            }

            var bar = _bars[i];
            // Smooth transition: lerp toward target
            double current = bar.Height;
            double lerped = current + 0.35 * (targetHeight - current);
            bar.Height = lerped;
            Canvas.SetTop(bar, canvasHeight - lerped);
        }
    }

    // ── Public API ───────────────────────────────────────────────────────

    /// <summary>
    /// Applies the overlay settings (opacity).
    /// Position is determined by global mouse hook, size is fixed for the pill.
    /// </summary>
    public void ApplySettings(OverlaySettings settings)
    {
        MaxOpacity = settings.Opacity;
    }

    /// <summary>
    /// Shows the overlay with a fade-in animation at the last clicked position.
    /// </summary>
    public void ShowOverlay()
    {
        if (_isVisible) return;
        _isVisible = true;

        PositionAtLastClick();
        Show();

        if (_fadeIn != null)
        {
            var anim = (DoubleAnimation)_fadeIn.Children[0];
            anim.To = MaxOpacity;
        }

        _fadeIn?.Begin(this);
        _barAnimationTimer?.Start();

        SetMicState(OverlayMicState.Idle);

        Trace.TraceInformation("[DictationOverlay] Shown at ({0}, {1}).", Left, Top);
    }

    /// <summary>
    /// Hides the overlay with a fade-out animation.
    /// </summary>
    public void HideOverlay()
    {
        if (!_isVisible) return;
        _isVisible = false;
        _currentState = OverlayMicState.Idle;
        _smoothedRms = 0;

        _barAnimationTimer?.Stop();

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
    /// Sets the visual state of the overlay.
    /// Must be called on the UI thread.
    /// </summary>
    public void SetMicState(OverlayMicState newState)
    {
        if (!_isVisible) return;
        if (_currentState == newState) return;

        var previousState = _currentState;
        _currentState = newState;

        switch (newState)
        {
            case OverlayMicState.Idle:
            case OverlayMicState.Speaking:
                AnimateBorderColor(BlueBorderColor);
                SetBarColor(OrangeBarColor);
                PillBorder.Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x2D, 0x2D, 0x2D));
                break;

            case OverlayMicState.NoMic:
                AnimateBorderColor(GreyColor);
                SetBarColor(GreyColor);
                PillBorder.Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x2D, 0x2D, 0x2D));
                _smoothedRms = 0;
                break;

            case OverlayMicState.Error:
                AnimateBorderColor(RedColor);
                SetBarColor(RedColor);
                PillBorder.Background = new SolidColorBrush(RedColor);
                _smoothedRms = 0;
                break;
        }

        Trace.TraceInformation("[DictationOverlay] State: {0} -> {1}", previousState, newState);
    }

    /// <summary>
    /// Updates the bar heights based on real-time RMS audio amplitude.
    /// Must be called on the UI thread. Only effective in Speaking state.
    /// </summary>
    public void UpdateAmplitude(double rmsAmplitude)
    {
        if (!_isVisible || _currentState != OverlayMicState.Speaking) return;

        rmsAmplitude = Math.Clamp(rmsAmplitude, 0.0, 1.0);
        _smoothedRms = _smoothedRms + RmsSmoothingFactor * (rmsAmplitude - _smoothedRms);
    }

    /// <summary>
    /// Convenience: Switches to the Speaking state.
    /// </summary>
    public void NotifySpeechActivity()
    {
        SetMicState(OverlayMicState.Speaking);
    }

    /// <summary>
    /// Convenience: Switches back to the Idle state.
    /// </summary>
    public void NotifySpeechPause()
    {
        SetMicState(OverlayMicState.Idle);
    }

    // ── Positioning ──────────────────────────────────────────────────────

    /// <summary>
    /// Positions the pill overlay at the last globally-clicked mouse position.
    /// The click position becomes the left edge of the pill; it extends to the right.
    /// </summary>
    private void PositionAtLastClick()
    {
        // Convert screen coordinates to WPF device-independent units
        var source = PresentationSource.FromVisual(this);
        double dpiScaleX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        double dpiScaleY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

        double screenX = _lastClickX * dpiScaleX;
        double screenY = _lastClickY * dpiScaleY;

        // Left-anchor the pill at the click position, offset slightly up so it doesn't cover the click
        Left = screenX;
        Top = screenY - Height - 5;

        // Ensure the pill stays within screen bounds
        var workArea = SystemParameters.WorkArea;
        if (Left + Width > workArea.Right)
            Left = workArea.Right - Width;
        if (Left < workArea.Left)
            Left = workArea.Left;
        if (Top < workArea.Top)
            Top = workArea.Top;
        if (Top + Height > workArea.Bottom)
            Top = workArea.Bottom - Height;
    }

    // ── Color helpers ────────────────────────────────────────────────────

    private void AnimateBorderColor(Color targetColor)
    {
        var anim = new ColorAnimation(targetColor, ColorTransitionDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        PillBorderBrush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
    }

    private void SetBarColor(Color color)
    {
        var brush = new SolidColorBrush(color);
        for (int i = 0; i < BarCount; i++)
        {
            _bars[i].Fill = brush;
        }
    }

    // ── Fade-out callback ────────────────────────────────────────────────

    private void OnFadeOutCompleted(object? sender, EventArgs e)
    {
        _fadeOut!.Completed -= OnFadeOutCompleted;
        Hide();
        Opacity = 0;
    }

    // ── Click-through ────────────────────────────────────────────────────

    private void SetClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }

    // ── Global mouse hook ────────────────────────────────────────────────

    private void InstallGlobalMouseHook()
    {
        _mouseHookProc = MouseHookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _mouseHookHandle = SetWindowsHookEx(
            WH_MOUSE_LL,
            _mouseHookProc,
            GetModuleHandle(curModule.ModuleName),
            0);

        if (_mouseHookHandle == IntPtr.Zero)
        {
            Trace.TraceWarning("[DictationOverlay] Failed to install global mouse hook. Error: {0}",
                Marshal.GetLastWin32Error());
        }
        else
        {
            Trace.TraceInformation("[DictationOverlay] Global mouse hook installed.");
        }
    }

    private void UninstallGlobalMouseHook()
    {
        if (_mouseHookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHookHandle);
            _mouseHookHandle = IntPtr.Zero;
            Trace.TraceInformation("[DictationOverlay] Global mouse hook uninstalled.");
        }
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (int)wParam == WM_LBUTTONDOWN)
        {
            var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            _lastClickX = hookStruct.pt.x;
            _lastClickY = hookStruct.pt.y;
        }

        return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
    }
}
