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
        var systemTemplates = SystemTemplateDefinitions.All;

        // Build candidate list: system templates first (take precedence), then user templates
        var allCandidates = new List<(string Name, bool IsSystem, string? ActionId)>();

        foreach (var st in systemTemplates)
        {
            if (!string.IsNullOrWhiteSpace(st.Name))
                allCandidates.Add((st.Name, true, st.ActionId));
        }

        foreach (var t in templates)
        {
            if (!string.IsNullOrWhiteSpace(t.Name))
                allCandidates.Add((t.Name, false, null));
        }

        if (allCandidates.Count == 0)
            return null;

        var candidateNames = allCandidates.Select(c => c.Name).ToList();
        var matchedName = FuzzyMatcher.FindBestMatch(spokenText, candidateNames);
        if (matchedName is null)
        {
            Trace.TraceInformation(
                "[TemplateService] No match found for spoken text: \"{0}\"", spokenText);
            return null;
        }

        var score = FuzzyMatcher.ComputeSimilarity(
            spokenText.Trim().ToLowerInvariant(),
            matchedName.Trim().ToLowerInvariant());

        // Check if the match is a system template
        var matchedCandidate = allCandidates.First(c =>
            string.Equals(c.Name, matchedName, StringComparison.OrdinalIgnoreCase));

        if (matchedCandidate.IsSystem)
        {
            Trace.TraceInformation(
                "[TemplateService] Matched \"{0}\" -> system template \"{1}\" (score={2:F2}, action={3})",
                spokenText, matchedName, score, matchedCandidate.ActionId);

            return new TemplateMatchResult(matchedName, string.Empty, score,
                IsSystemTemplate: true, SystemActionId: matchedCandidate.ActionId);
        }

        var template = templates.First(t =>
            string.Equals(t.Name, matchedName, StringComparison.OrdinalIgnoreCase));

        var expandedText = TemplatePlaceholderExpander.Expand(template.Text);

        Trace.TraceInformation(
            "[TemplateService] Matched \"{0}\" -> template \"{1}\" (score={2:F2})",
            spokenText, matchedName, score);

        return new TemplateMatchResult(matchedName, expandedText, score);
    }

    /// <summary>The sentinel name for the default ungrouped group.</summary>
    public const string UngroupedName = "Ungrouped";

    /// <inheritdoc />
    public IReadOnlyList<TemplateItem> GetTemplates()
    {
        return _settingsService.Current.Templates.Items.AsReadOnly();
    }

    /// <inheritdoc />
    public void AddTemplate(string name, string text, string? group = null)
    {
        _settingsService.Current.Templates.Items.Add(new TemplateItem
        {
            Name = name,
            Text = text,
            Group = string.IsNullOrWhiteSpace(group) || group == UngroupedName ? null : group
        });
        _settingsService.Save();
    }

    /// <inheritdoc />
    public void UpdateTemplate(int index, string name, string text)
    {
        var items = _settingsService.Current.Templates.Items;
        if (index < 0 || index >= items.Count)
            return;

        var existing = items[index];
        items[index] = new TemplateItem { Name = name, Text = text, Group = existing.Group };
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

    /// <inheritdoc />
    public void MoveTemplateToGroup(int templateIndex, string? groupName)
    {
        var items = _settingsService.Current.Templates.Items;
        if (templateIndex < 0 || templateIndex >= items.Count)
            return;

        items[templateIndex].Group =
            string.IsNullOrWhiteSpace(groupName) || groupName == UngroupedName ? null : groupName;
        _settingsService.Save();
    }

    /// <inheritdoc />
    public IReadOnlyList<TemplateGroup> GetGroups()
    {
        EnsureDefaults();
        return _settingsService.Current.Templates.Groups
            .OrderBy(g => g.Name == UngroupedName ? 0
                : string.Equals(g.Name, SystemTemplateDefinitions.SystemGroupName, StringComparison.OrdinalIgnoreCase) ? 2
                : 1)
            .ThenBy(g => g.Order)
            .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public void AddGroup(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var groups = _settingsService.Current.Templates.Groups;

        // No duplicates
        if (groups.Any(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
            return;

        var maxOrder = groups.Count > 0 ? groups.Max(g => g.Order) : 0;
        groups.Add(new TemplateGroup { Name = name, IsExpanded = true, Order = maxOrder + 1 });
        _settingsService.Save();
    }

    /// <inheritdoc />
    public void RenameGroup(string oldName, string newName)
    {
        if (string.Equals(oldName, UngroupedName, StringComparison.OrdinalIgnoreCase)) return;
        if (string.Equals(oldName, SystemTemplateDefinitions.SystemGroupName, StringComparison.OrdinalIgnoreCase)) return;
        if (string.IsNullOrWhiteSpace(newName)) return;

        var groups = _settingsService.Current.Templates.Groups;
        var group = groups.FirstOrDefault(g => string.Equals(g.Name, oldName, StringComparison.OrdinalIgnoreCase));
        if (group is null) return;

        // Update template references
        foreach (var item in _settingsService.Current.Templates.Items)
        {
            if (string.Equals(item.Group, oldName, StringComparison.OrdinalIgnoreCase))
                item.Group = newName;
        }

        group.Name = newName;
        _settingsService.Save();
    }

    /// <inheritdoc />
    public bool RemoveGroup(string name)
    {
        if (string.Equals(name, UngroupedName, StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(name, SystemTemplateDefinitions.SystemGroupName, StringComparison.OrdinalIgnoreCase)) return false;

        var templates = _settingsService.Current.Templates.Items;
        var hasTemplates = templates.Any(t =>
            string.Equals(t.Group, name, StringComparison.OrdinalIgnoreCase));
        if (hasTemplates) return false;

        var groups = _settingsService.Current.Templates.Groups;
        var removed = groups.RemoveAll(g =>
            string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
            _settingsService.Save();
        return removed > 0;
    }

    /// <inheritdoc />
    public void ReorderGroups(IReadOnlyList<string> groupNamesInOrder)
    {
        var groups = _settingsService.Current.Templates.Groups;
        for (var i = 0; i < groupNamesInOrder.Count; i++)
        {
            var g = groups.FirstOrDefault(x =>
                string.Equals(x.Name, groupNamesInOrder[i], StringComparison.OrdinalIgnoreCase));
            if (g is not null)
                g.Order = g.Name == UngroupedName ? 0
                    : string.Equals(g.Name, SystemTemplateDefinitions.SystemGroupName, StringComparison.OrdinalIgnoreCase) ? int.MaxValue
                    : i + 1;
        }
        _settingsService.Save();
    }

    /// <inheritdoc />
    public void SetGroupExpanded(string groupName, bool isExpanded)
    {
        var groups = _settingsService.Current.Templates.Groups;
        var group = groups.FirstOrDefault(g =>
            string.Equals(g.Name, groupName, StringComparison.OrdinalIgnoreCase));
        if (group is not null)
        {
            group.IsExpanded = isExpanded;
            _settingsService.Save();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SystemTemplate> GetSystemTemplates()
    {
        return SystemTemplateDefinitions.All;
    }

    /// <inheritdoc />
    public void EnsureDefaults()
    {
        var settings = _settingsService.Current.Templates;
        var groups = settings.Groups;

        // Ensure "Ungrouped" group always exists
        if (!groups.Any(g => string.Equals(g.Name, UngroupedName, StringComparison.OrdinalIgnoreCase)))
        {
            groups.Insert(0, new TemplateGroup { Name = UngroupedName, IsExpanded = true, Order = 0 });
        }

        // Migrate existing templates: any with null/empty group → Ungrouped (stored as null)
        // No action needed since null group is interpreted as Ungrouped
    }
}
