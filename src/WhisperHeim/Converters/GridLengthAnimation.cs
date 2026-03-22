using System.Windows;
using System.Windows.Media.Animation;

namespace WhisperHeim.Converters;

/// <summary>
/// Animates a <see cref="GridLength"/> value between two pixel widths.
/// WPF does not include a built-in GridLength animation, so this provides one
/// for smooth sidebar collapse/expand transitions.
/// </summary>
public sealed class GridLengthAnimation : AnimationTimeline
{
    public static readonly DependencyProperty FromProperty =
        DependencyProperty.Register(nameof(From), typeof(GridLength), typeof(GridLengthAnimation));

    public static readonly DependencyProperty ToProperty =
        DependencyProperty.Register(nameof(To), typeof(GridLength), typeof(GridLengthAnimation));

    public static readonly DependencyProperty EasingFunctionProperty =
        DependencyProperty.Register(nameof(EasingFunction), typeof(IEasingFunction), typeof(GridLengthAnimation));

    public GridLength From
    {
        get => (GridLength)GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    public GridLength To
    {
        get => (GridLength)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    public IEasingFunction? EasingFunction
    {
        get => (IEasingFunction?)GetValue(EasingFunctionProperty);
        set => SetValue(EasingFunctionProperty, value);
    }

    public override Type TargetPropertyType => typeof(GridLength);

    protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
    {
        var from = From.Value;
        var to = To.Value;

        var progress = animationClock.CurrentProgress ?? 0.0;

        if (EasingFunction is { } easing)
        {
            progress = easing.Ease(progress);
        }

        var current = from + (to - from) * progress;
        return new GridLength(current, GridUnitType.Pixel);
    }
}
