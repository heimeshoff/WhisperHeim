namespace WhisperHeim.Models;

/// <summary>
/// A built-in system template that provides control functionality rather than text expansion.
/// System templates are immutable and defined in code, not persisted in settings.
/// </summary>
public sealed class SystemTemplate
{
    /// <summary>Trigger name for fuzzy matching.</summary>
    public string Name { get; }

    /// <summary>Human-readable description of what the command does.</summary>
    public string Description { get; }

    /// <summary>Unique action identifier used by the orchestrator to dispatch behavior.</summary>
    public string ActionId { get; }

    public SystemTemplate(string name, string description, string actionId)
    {
        Name = name;
        Description = description;
        ActionId = actionId;
    }
}

/// <summary>
/// Static registry of all built-in system templates.
/// </summary>
public static class SystemTemplateDefinitions
{
    /// <summary>Action ID for the Repeat command.</summary>
    public const string RepeatActionId = "system.repeat";

    /// <summary>Display name of the WhisperHeim system group.</summary>
    public const string SystemGroupName = "WhisperHeim";

    /// <summary>All built-in system templates.</summary>
    public static IReadOnlyList<SystemTemplate> All { get; } = new[]
    {
        new SystemTemplate("Repeat", "Types the last dictated text again", RepeatActionId),
    };
}
