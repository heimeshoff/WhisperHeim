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
    void AddTemplate(string name, string text, string? group = null);

    /// <summary>
    /// Updates an existing template at the specified index.
    /// </summary>
    void UpdateTemplate(int index, string name, string text);

    /// <summary>
    /// Removes the template at the specified index.
    /// </summary>
    void RemoveTemplate(int index);

    /// <summary>
    /// Moves a template to a different group.
    /// </summary>
    void MoveTemplateToGroup(int templateIndex, string? groupName);

    /// <summary>
    /// Gets all template groups in display order. Always includes "Ungrouped" first.
    /// </summary>
    IReadOnlyList<TemplateGroup> GetGroups();

    /// <summary>
    /// Adds a new template group.
    /// </summary>
    void AddGroup(string name);

    /// <summary>
    /// Renames a template group (cannot rename "Ungrouped").
    /// </summary>
    void RenameGroup(string oldName, string newName);

    /// <summary>
    /// Removes an empty template group (cannot remove "Ungrouped").
    /// </summary>
    bool RemoveGroup(string name);

    /// <summary>
    /// Reorders groups by setting their Order property.
    /// "Ungrouped" always stays at position 0.
    /// </summary>
    void ReorderGroups(IReadOnlyList<string> groupNamesInOrder);

    /// <summary>
    /// Updates the expanded state of a group.
    /// </summary>
    void SetGroupExpanded(string groupName, bool isExpanded);

    /// <summary>
    /// Ensures migration: existing templates without a group go to "Ungrouped",
    /// and the "Ungrouped" group always exists.
    /// </summary>
    void EnsureDefaults();
}
