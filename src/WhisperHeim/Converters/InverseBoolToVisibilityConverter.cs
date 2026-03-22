using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WhisperHeim.Converters;

/// <summary>
/// Converts a boolean to Visibility, inverting the logic:
/// true → Collapsed, false → Visible.
/// </summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility.Collapsed;
    }
}
