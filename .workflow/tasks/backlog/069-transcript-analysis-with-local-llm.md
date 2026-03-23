# Task 069: Transcript Analysis with Local LLM

**Status:** Backlog
**Size:** Medium
**Milestone:** Milestone 2 (Audio Capture + Call Transcription)
**Dependencies:** None

## Description

Add AI-powered analysis of recorded transcripts using a local LLM via Ollama. Users can define reusable prompt templates (e.g., "Action Items", "Key Decisions", "Ideas", "Meeting Summary") and run them against any recording's transcript. Results stream in real-time.

This avoids cloud API costs entirely — the user runs Ollama locally with an open-source model like Qwen 2.5 14B, consistent with WhisperHeim's local-first, zero-cost philosophy.

## Research

See `.workflow/research/transcript-analysis-with-llm.md` for full findings.

- Claude subscription cannot be used programmatically (blocked by Anthropic since Jan 2026)
- Ollama + Qwen 2.5 14B is the best local alternative (~80-90% of GPT-4 quality for extraction tasks)
- OllamaSharp NuGet provides `IChatClient` via Microsoft.Extensions.AI
- 1.5h transcripts (~18K tokens) fit in Qwen 2.5's 128K context window without chunking

## Implementation Outline

### 1. Ollama Integration Layer
- Add **OllamaSharp** NuGet package
- Service class wrapping `OllamaApiClient` with `IChatClient`
- Connection test / health check via Ollama's `/api/tags` endpoint
- Streaming response support

### 2. Settings
- Ollama endpoint URL (default `http://localhost:11434`)
- Model selector — auto-detect installed models via `/api/tags`
- Connection status indicator

### 3. Prompt Templates
- User-defined prompts stored in app settings or a JSON file
- Each template has a **title** and a **prompt body** (with `{transcript}` placeholder)
- Ship with a few built-in defaults: Action Items, Key Decisions, Ideas, Meeting Summary
- UI to create, edit, and delete templates

### 4. Analysis UI on Transcripts Page
- "Analyze" button/dropdown on selected recording
- Shows available prompt templates
- Selecting one sends the full Markdown transcript + prompt to Ollama
- Response streams into a result panel (Markdown rendered)
- Option to copy result or save alongside the recording

## Acceptance Criteria

- [ ] OllamaSharp integrated, connection to local Ollama verified
- [ ] Settings page allows configuring Ollama URL and selecting a model
- [ ] User can create, edit, and delete prompt templates
- [ ] Built-in default templates ship with the app
- [ ] "Analyze" action available on transcripts page for any recording
- [ ] Analysis result streams in real-time
- [ ] Works fully offline with no cloud dependency
- [ ] Graceful error handling when Ollama is not running or model not installed

## Files to Modify/Create

- New: `Services/OllamaService.cs` (or similar — Ollama integration)
- New: `Models/PromptTemplate.cs` (template data model)
- Modify: `Views/Pages/TranscriptsPage.xaml` + `.xaml.cs` (add Analyze UI)
- Modify: Settings page (Ollama configuration)
- Modify: App settings / config (template storage, Ollama settings)
