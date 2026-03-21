using System.Diagnostics;
using WhisperHeim.Models;
using WhisperHeim.Services.Settings;

namespace WhisperHeim.Services.Templates;

/// <summary>
/// Manages templates stored in settings and provides fuzzy matching
/// of spoken names to template text with placeholder expansion.
/// </summary>
public sealed class TemplateService : ITemplateService
{
    private readonly SettingsService _settingsService;

    public TemplateService(SettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    /// <inheritdoc />
    public TemplateMatchResult? MatchAndExpand(string spokenText)
    {
        if (string.IsNullOrWhiteSpace(spokenText))
            return null;

        var templates = _settingsService.Current.Templates.Items;
        if (templates.Count == 0)
            return null;

        var candidateNames = templates
            .Where(t => !string.IsNullOrWhiteSpace(t.Name))
            .Select(t => t.Name)
            .ToList();

        var matchedName = FuzzyMatcher.FindBestMatch(spokenText, candidateNames);
        if (matchedName is null)
        {
            Trace.TraceInformation(
                "[TemplateService] No match found for spoken text: \"{0}\"", spokenText);
            return null;
        }

        var template = templates.First(t =>
            string.Equals(t.Name, matchedName, StringComparison.OrdinalIgnoreCase));

        var expandedText = TemplatePlaceholderExpander.Expand(template.Text);
        var score = FuzzyMatcher.ComputeSimilarity(
            spokenText.Trim().ToLowerInvariant(),
            matchedName.Trim().ToLowerInvariant());

        Trace.TraceInformation(
            "[TemplateService] Matched \"{0}\" -> template \"{1}\" (score={2:F2})",
            spokenText, matchedName, score);

        return new TemplateMatchResult(matchedName, expandedText, score);
    }

    /// <inheritdoc />
    public IReadOnlyList<TemplateItem> GetTemplates()
    {
        return _settingsService.Current.Templates.Items.AsReadOnly();
    }

    /// <inheritdoc />
    public void AddTemplate(string name, string text)
    {
        _settingsService.Current.Templates.Items.Add(new TemplateItem
        {
            Name = name,
            Text = text
        });
        _settingsService.Save();
    }

    /// <inheritdoc />
    public void UpdateTemplate(int index, string name, string text)
    {
        var items = _settingsService.Current.Templates.Items;
        if (index < 0 || index >= items.Count)
            return;

        items[index] = new TemplateItem { Name = name, Text = text };
        _settingsService.Save();
    }

    /// <inheritdoc />
    public void RemoveTemplate(int index)
    {
        var items = _settingsService.Current.Templates.Items;
        if (index < 0 || index >= items.Count)
            return;

        items.RemoveAt(index);
        _settingsService.Save();
    }
}
