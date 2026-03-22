namespace WhisperHeim.Services.Templates;

/// <summary>
/// Expands placeholders in template text.
/// Supported placeholders: {date}, {time}.
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

        return result;
    }
}
