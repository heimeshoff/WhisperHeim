# Research: Transcript Analysis with LLM

**Date:** 2026-03-23
**Status:** Complete
**Relevance:** Milestone 2 (Audio Capture + Call Transcription) — adding AI-powered analysis of recorded transcripts

## Summary

The user wants to analyze long transcripts (up to 1.5h recordings) with AI prompts (action items, ideas, summaries, etc.) from within WhisperHeim. The key constraint: no API costs beyond their existing Anthropic subscription.

**Verdict: Anthropic's Claude subscription cannot be used programmatically.** As of January 2026, Anthropic actively blocks OAuth tokens from consumer plans in any third-party application. The recommended alternative is **Ollama with a local LLM** — zero cost, full privacy (aligns with WhisperHeim's local-first vision), and excellent .NET integration via OllamaSharp.

## Key Findings

### 1. Claude Subscription Cannot Be Used Programmatically

Anthropic's consumer plans (Pro $20/mo, Max $100-200/mo) are explicitly restricted to claude.ai and Claude Code only. Since January 9, 2026, server-side enforcement blocks subscription OAuth tokens from working in any third-party tool. A February 2026 documentation update made this a formal policy. The API is a separate product with per-token billing ($3-5/M input, $15-25/M output depending on model).

**Bottom line:** Building a "use my subscription" feature is not possible — it violates Anthropic's terms and is technically blocked.

### 2. Local LLMs via Ollama — The Best Alternative

Ollama runs open-source LLMs locally with automatic GPU acceleration, quantization, and a simple REST API. It aligns perfectly with WhisperHeim's local-first, zero-cost philosophy.

**Best models for transcript analysis (2026):**

| Model | Params | VRAM (Q4_K_M) | Strengths |
|-------|--------|---------------|-----------|
| **Qwen 2.5 14B** | 14B | ~10-12 GB | Best instruction following, structured extraction, strong at "3 bullet points" type prompts |
| **Qwen 3 8B** | 8B | ~6-8 GB | Good quality, lower VRAM, faster inference |
| **Llama 4 Scout** | 17B active (109B total) | ~12 GB | Strong reasoning, MoE architecture |
| **Gemma 3 12B** | 12B | ~8-10 GB | Fast inference, good for real-time recap |

Qwen 2.5 14B is the top recommendation for transcript summarization — it reaches ~80-90% of GPT-4 quality on structured extraction tasks and excels at following specific output format instructions.

### 3. .NET Integration Is Mature

**OllamaSharp** (NuGet) is the standard .NET SDK for Ollama:
- Implements `IChatClient` from `Microsoft.Extensions.AI`
- Covers every Ollama API endpoint (chat, embeddings, model management)
- Supports streaming responses, tool calling, multi-modality
- Native AOT compatible
- Used by Microsoft Semantic Kernel and .NET Aspire

Basic usage:
```csharp
using Microsoft.Extensions.AI;
using OllamaSharp;

IChatClient client = new OllamaApiClient(
    new Uri("http://localhost:11434/"), "qwen2.5:14b");

var response = await client.GetResponseAsync(
    "Extract action items from this transcript:\n\n" + transcriptText);
```

### 4. Context Window Considerations

A 1.5-hour transcript at ~150 words/minute ≈ 13,500 words ≈ ~18,000 tokens. Qwen 2.5 14B supports 128K context, so even long transcripts fit comfortably with room for the system prompt and response. No chunking needed.

### 5. Ollama Prerequisites for Users

- User must install Ollama separately (one-time ~200MB download)
- User must pull the model once (`ollama pull qwen2.5:14b`, ~9GB download)
- Ollama runs as a background service on localhost:11434
- Requires a GPU with ≥10GB VRAM for 14B models, or ≥6GB for 8B models
- CPU-only fallback works but is significantly slower (~10-30s per response vs ~2-5s with GPU)

## Implications for This Project

### Recommended Architecture

1. **Settings page**: Configure Ollama endpoint URL (default `http://localhost:11434`) and model name
2. **Prompt templates**: User-defined prompts with a title (e.g., "Action Items", "Key Decisions", "Ideas", "Meeting Summary"). Store in app settings or a JSON file.
3. **Analysis UI on Transcripts page**: After selecting a recording, an "Analyze" button/dropdown shows available prompt templates. Selecting one sends the full transcript + prompt to Ollama and displays the result.
4. **Model auto-detection**: Use Ollama's `/api/tags` endpoint to list installed models and let the user pick one.
5. **Streaming response**: Show the analysis result as it streams in, similar to a chat UI.

### Why This Fits WhisperHeim

- **Zero cost** — no API fees, no subscriptions beyond what user already has
- **Fully local** — consistent with the privacy-first, no-cloud vision
- **User owns the models** — no dependency on external service availability
- **Extensible** — user can swap models, try different sizes, or use fine-tuned variants

### Trade-offs

- Requires Ollama installation (additional setup step for users)
- Quality is ~80-90% of Claude/GPT-4 for extraction tasks — good enough for action items and summaries, may be less nuanced for complex analysis
- Needs a decent GPU (most users who run WhisperHeim already have one for Parakeet ASR)

## Open Questions

- Should WhisperHeim bundle/auto-install Ollama, or require users to install it separately?
- Should the app suggest a default model and offer to pull it (like it does with ASR models)?
- Should analysis results be persisted alongside the recording, or treated as ephemeral?
- Should there be a "custom prompt" option in addition to templates?

## Sources

- [Anthropic: Why subscription and API are separate](https://support.anthropic.com/en/articles/9876003-i-subscribe-to-a-paid-claude-ai-plan-why-do-i-have-to-pay-separately-for-api-usage-on-console)
- [Anthropic bans OAuth in third-party apps (WinBuzzer)](https://winbuzzer.com/2026/02/19/anthropic-bans-claude-subscription-oauth-in-third-party-apps-xcxwbn/)
- [Anthropic clarifies ban (The Register)](https://www.theregister.com/2026/02/20/anthropic_clarifies_ban_third_party_claude_access/)
- [OllamaSharp on GitHub](https://github.com/awaescher/OllamaSharp)
- [OllamaSharp on NuGet](https://www.nuget.org/packages/OllamaSharp)
- [Microsoft.Extensions.AI with Ollama guide](https://www.milanjovanovic.tech/blog/working-with-llms-in-dotnet-using-microsoft-extensions-ai)
- [GPT-OSS C# Guide with Ollama (.NET Blog)](https://devblogs.microsoft.com/dotnet/gpt-oss-csharp-ollama/)
- [Best Local LLMs for Summarization (InsiderLLM)](https://insiderllm.com/guides/best-local-llms-summarization/)
- [Best LLMs for Summarization 2026 (ClickUp)](https://clickup.com/blog/best-llms-for-language-summarization/)
- [Ollama VRAM Requirements Guide](https://localllm.in/blog/ollama-vram-requirements-for-local-llms)
- [Qwen 2.5 14B VRAM specs](https://apxml.com/models/qwen2-5-14b)
- [Local meeting notes with Ollama (Meetily)](https://dev.to/zackriya/local-meeting-notes-with-whisper-transcription-ollama-summaries-gemma3n-llama-mistral--2i3n)
