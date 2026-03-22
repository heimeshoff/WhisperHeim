using WhisperHeim.Models;

namespace WhisperHeim.Services.Templates;

/// <summary>
/// Result of a template match operation.
/// </summary>
public sealed record TemplateMatchResult(
    string TemplateName,
    string ExpandedText,
    double MatchScore);

/// <summary>
/// Manages templates and matches spoken names to template text.
/// </summary>
public interface ITemplateService
{
    /// <summary>
    /// Attempts to match a spoken phrase to a template name,
    /// and returns the expanded template text if matched.
    /// </summary>
    /// <param name="spokenText">Transcribed speech to match against template names.</param>
    /// <returns>Match result, or null if no template matched.</returns>
    TemplateMatchResult? MatchAndExpand(string spokenText);

    /// <summary>
    /// Gets the current list of templates from settings.
    /// </summary>
    IReadOnlyList<TemplateItem> GetTemplates();

    /// <summary>
    /// Adds a new template.
    /// </summary>
    void AddTemplate(string name, string text);

    /// <summary>
    /// Updates an existing template at the specified index.
    /// </summary>
    void UpdateTemplate(int index, string name, string text);

    /// <summary>
    /// Removes the template at the specified index.
    /// </summary>
    void RemoveTemplate(int index);
}
