using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using WhisperHeim.Models;
using WhisperHeim.Services.Settings;
using OllamaSharp;
using Microsoft.Extensions.AI;

namespace WhisperHeim.Services.Analysis;

/// <summary>
/// Wraps Ollama API access for local LLM transcript analysis.
/// Provides connection testing, model listing, and streaming chat completion.
/// </summary>
public sealed class OllamaService
{
    private readonly SettingsService _settingsService;
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromMinutes(10) };

    /// <summary>Built-in prompt templates shipped with the app.</summary>
    public static readonly IReadOnlyList<AnalysisPromptTemplate> DefaultTemplates =
    [
        new()
        {
            Id = "builtin-action-items",
            Title = "Action Items",
            Prompt = "Extract all action items from this transcript. For each action item, identify WHO is responsible and WHAT they need to do. Format as a numbered list.\n\n{transcript}",
            IsBuiltIn = true
        },
        new()
        {
            Id = "builtin-key-decisions",
            Title = "Key Decisions",
            Prompt = "List all key decisions made during this conversation. For each decision, briefly note the context and rationale if mentioned. Format as a bulleted list.\n\n{transcript}",
            IsBuiltIn = true
        },
        new()
        {
            Id = "builtin-ideas",
            Title = "Ideas",
            Prompt = "Extract all ideas, suggestions, and proposals mentioned in this transcript. Include who suggested each idea if identifiable. Format as a bulleted list.\n\n{transcript}",
            IsBuiltIn = true
        },
        new()
        {
            Id = "builtin-meeting-summary",
            Title = "Meeting Summary",
            Prompt = "Write a concise summary of this meeting/conversation. Include:\n- Main topics discussed\n- Key decisions made\n- Action items assigned\n- Any open questions or follow-ups needed\n\nKeep the summary to 3-5 paragraphs.\n\n{transcript}",
            IsBuiltIn = true
        }
    ];

    public OllamaService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        EnsureDefaultTemplates();

        // After a disk reload swapped AppSettings underneath us, built-in templates
        // may be missing if another machine's file didn't have them yet. Re-ensure
        // them on every change so the analysis UI always shows the defaults.
        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        // Only on disk reloads — local saves go through AddTemplate/UpdateTemplate
        // and already have the right state.
        if (e.Source != SettingsChangeSource.DiskReload) return;
        EnsureDefaultTemplates();
    }

    /// <summary>
    /// Ensures built-in templates exist in settings. Called on construction and
    /// can be called if the user resets templates.
    /// </summary>
    public void EnsureDefaultTemplates()
    {
        var templates = _settingsService.Current.Ollama.AnalysisTemplates;
        foreach (var defaultTemplate in DefaultTemplates)
        {
            if (!templates.Any(t => t.Id == defaultTemplate.Id))
            {
                templates.Add(new AnalysisPromptTemplate
                {
                    Id = defaultTemplate.Id,
                    Title = defaultTemplate.Title,
                    Prompt = defaultTemplate.Prompt,
                    IsBuiltIn = true
                });
            }
        }
    }

    /// <summary>
    /// Gets the configured Ollama endpoint URL.
    /// </summary>
    public string Endpoint => _settingsService.Current.Ollama.Endpoint;

    /// <summary>
    /// Gets the configured model name, or null if none selected.
    /// </summary>
    public string? Model => _settingsService.Current.Ollama.Model;

    /// <summary>
    /// Tests connectivity to the Ollama server.
    /// Returns true if the server is reachable and responds.
    /// </summary>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var client = CreateClient();
            var models = await client.ListLocalModelsAsync();
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[OllamaService] Connection test failed: {0}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Lists all models installed on the Ollama server.
    /// Returns empty list if the server is unreachable.
    /// </summary>
    public async Task<List<string>> ListLocalModelsAsync()
    {
        try
        {
            var client = CreateClient();
            var models = await client.ListLocalModelsAsync();
            return models
                .Select(m => m.Name)
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n)
                .ToList();
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[OllamaService] Failed to list models: {0}", ex.Message);
            return [];
        }
    }

    /// <summary>
    /// Runs an analysis prompt template against a transcript, streaming results.
    /// </summary>
    /// <param name="template">The prompt template to use.</param>
    /// <param name="transcriptMarkdown">The full transcript in Markdown format.</param>
    /// <param name="onToken">Callback invoked for each streamed token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete response text.</returns>
    public async Task<string> AnalyzeAsync(
        AnalysisPromptTemplate template,
        string transcriptMarkdown,
        Action<string> onToken,
        CancellationToken cancellationToken = default)
    {
        var model = _settingsService.Current.Ollama.Model;
        if (string.IsNullOrEmpty(model))
            throw new InvalidOperationException("No Ollama model selected. Please configure a model in Settings.");

        var prompt = template.Prompt.Replace("{transcript}", transcriptMarkdown);

        var client = CreateClient();
        client.SelectedModel = model;

        IChatClient chatClient = client;
        var fullResponse = new System.Text.StringBuilder();

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, prompt)
        };

        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, cancellationToken: cancellationToken))
        {
            var text = update.Text;
            if (!string.IsNullOrEmpty(text))
            {
                fullResponse.Append(text);
                onToken(text);
            }
        }

        return fullResponse.ToString();
    }

    /// <summary>
    /// Gets all configured analysis templates (built-in + user-defined).
    /// </summary>
    public List<AnalysisPromptTemplate> GetTemplates()
    {
        return _settingsService.Current.Ollama.AnalysisTemplates;
    }

    /// <summary>
    /// Adds a new user-defined template and saves settings.
    /// Reloads from disk first so concurrent additions from another machine
    /// are preserved through the merge in <see cref="SettingsService.Save"/>.
    /// </summary>
    public void AddTemplate(AnalysisPromptTemplate template)
    {
        _settingsService.ReloadFromDiskForMutation();
        _settingsService.Current.Ollama.AnalysisTemplates.Add(template);
        _settingsService.Save();
    }

    /// <summary>
    /// Updates an existing template and saves settings.
    /// </summary>
    public void UpdateTemplate(AnalysisPromptTemplate template)
    {
        _settingsService.ReloadFromDiskForMutation();
        var templates = _settingsService.Current.Ollama.AnalysisTemplates;
        var index = templates.FindIndex(t => t.Id == template.Id);
        if (index >= 0)
        {
            templates[index] = template;
            _settingsService.Save();
        }
    }

    /// <summary>
    /// Deletes a template by ID. Built-in templates cannot be deleted.
    /// </summary>
    public bool DeleteTemplate(string id)
    {
        _settingsService.ReloadFromDiskForMutation();
        var templates = _settingsService.Current.Ollama.AnalysisTemplates;
        var template = templates.FirstOrDefault(t => t.Id == id);
        if (template is null || template.IsBuiltIn)
            return false;

        templates.Remove(template);
        _settingsService.Save();
        return true;
    }

    private OllamaApiClient CreateClient()
    {
        var endpoint = _settingsService.Current.Ollama.Endpoint;
        var uri = new Uri(endpoint);
        return new OllamaApiClient(uri);
    }
}
