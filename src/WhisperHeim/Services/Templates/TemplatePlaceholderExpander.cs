using System.Windows;

namespace WhisperHeim.Services.Templates;

/// <summary>
/// Expands placeholders in template text.
/// Supported placeholders: {date}, {time}, {clipboard}.
/// </summary>
public static class TemplatePlaceholderExpander
{
    /// <summary>
    /// Expands all known placeholders in the given template text.
    /// </summary>
    public static string Expand(string templateText)
    {
        if (string.IsNullOrEmpty(templateText))
            return templateText;

        var result = templateText;

        // {date} -> current date in local format
        result = result.Replace("{date}", DateTime.Now.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase);

        // {time} -> current time in local format
        result = result.Replace("{time}", DateTime.Now.ToString("HH:mm"), StringComparison.OrdinalIgnoreCase);

        // {clipboard} -> current clipboard text
        if (result.Contains("{clipboard}", StringComparison.OrdinalIgnoreCase))
        {
            var clipboardText = GetClipboardText();
            result = result.Replace("{clipboard}", clipboardText ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static string? GetClipboardText()
    {
        try
        {
            if (Clipboard.ContainsText())
                return Clipboard.GetText();
        }
        catch
        {
            // Clipboard may be locked by another app
        }

        return null;
    }
}
