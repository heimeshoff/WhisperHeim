using System.Text.Json.Serialization;

namespace WhisperHeim.Models;

/// <summary>
/// A reusable prompt template for AI-powered transcript analysis via Ollama.
/// The prompt body may contain a <c>{transcript}</c> placeholder that will be
/// replaced with the full transcript text before being sent to the LLM.
/// </summary>
public sealed class AnalysisPromptTemplate
{
    /// <summary>Unique identifier for this template.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Display title (e.g. "Action Items", "Meeting Summary").</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The prompt body. Use <c>{transcript}</c> as a placeholder for the transcript text.
    /// </summary>
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Whether this is a built-in template that cannot be deleted.</summary>
    [JsonPropertyName("isBuiltIn")]
    public bool IsBuiltIn { get; set; }
}
