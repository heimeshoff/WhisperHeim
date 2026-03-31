using System.Globalization;
using System.Windows.Data;

namespace WhisperHeim.Converters;

/// <summary>
/// Collapses multi-line text into a single line by replacing newline
/// sequences with a space. Used for template description previews in lists.
/// </summary>
public sealed class SingleLineTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string text)
            return string.Empty;

        // Replace all CR/LF combinations with a single space and trim
        return text
            .Replace("\r\n", " ")
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
