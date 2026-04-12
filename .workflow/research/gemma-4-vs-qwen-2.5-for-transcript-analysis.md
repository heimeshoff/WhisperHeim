# Research: Gemma 4 vs Qwen 2.5 14B for Transcript Analysis

**Date:** 2026-04-09
**Status:** Complete
**Relevance:** Milestone 2 — LLM-powered transcript analysis (action items, summaries, key decisions)

## Summary

Google released **Gemma 4** on April 2, 2026 with four sizes: E2B, E4B, 26B MoE, and 31B Dense. The headline claim is "unprecedented intelligence-per-parameter" — the 31B Dense model ranks #3 on Arena AI among all open models, beating models 20x its size. However, for WhisperHeim running on an **RTX 3080 with 10GB VRAM**, only the E4B variant fits comfortably, and it scores significantly lower than the current Qwen 2.5 14B on all relevant benchmarks.

**Verdict: Stick with Qwen 2.5 14B.** The Gemma 4 models that match or exceed Qwen 2.5 14B quality (26B MoE, 31B Dense) require 15-18+ GB VRAM and don't fit on this GPU. The E4B model that does fit is ~15 percentage points behind on MMLU and substantially weaker on instruction following — critical for structured transcript extraction tasks.

## Key Findings

### 1. Gemma 4 Model Lineup & VRAM Requirements

| Model | Total Params | Effective Params | VRAM (Q4_K_M) | Fits RTX 3080 10GB? | Context |
|-------|-------------|-----------------|----------------|---------------------|---------|
| E2B | ~5B | 2B | ~1.5 GB | Yes | 32K |
| **E4B** | ~8B | 4B | **~2.5 GB** | **Yes** | 32K |
| 26B MoE | 26B | ~8B active | ~15 GB | No | 128K |
| 31B Dense | 31B | 31B | ~18 GB | No | 128K |

The E4B uses "selective parameter activation" — 8B total parameters but only 4B active at any time. This makes it very fast and lightweight, but it's effectively a 4B-class model in capability.

### 2. Benchmark Comparison: Gemma 4 E4B vs Qwen 2.5 14B

| Benchmark | Gemma 4 E4B | Qwen 2.5 14B | Winner |
|-----------|------------|-------------|--------|
| MMLU | 69.4% | 79.7% | Qwen 2.5 14B (+10.3) |
| MMLU-Pro | ~50.6%* | ~65%+ | Qwen 2.5 14B |
| GPQA Diamond | ~23.7%* | ~45%+ | Qwen 2.5 14B |
| HumanEval | 75.0% | 72.5% | Gemma 4 E4B (+2.5) |
| Context Window | 32K | 128K | Qwen 2.5 14B |

*Gemma 3n E4B scores used as proxy — Gemma 4 E4B likely slightly better but same architecture class.

Qwen 2.5 14B dominates on MMLU and reasoning benchmarks. The only area where E4B wins is code generation (HumanEval), which is irrelevant for transcript analysis.

### 3. Instruction Following — The Critical Gap

For transcript analysis (extracting action items, summaries, key decisions), **instruction following** is the most important capability. The IFEval benchmark measures this directly:

- **Qwen 3.5-4B** (same size class as Gemma 4 E4B): **89.8%** on IFEval
- **Gemma 3n E4B**: **57.2%** on IFEval (Include metric)

Even the newer Qwen at the same parameter count crushes Gemma at structured instruction following. Qwen 2.5 14B, being 3.5x larger, has an even bigger advantage. This matters enormously for prompts like "Extract 3 action items as bullet points" or "Summarize key decisions in this format."

### 4. Context Window Limitation

A 1.5-hour transcript ≈ 18,000 tokens. Gemma 4 E4B's 32K context window can handle this, but with less headroom for system prompts and responses than Qwen 2.5 14B's 128K window. For shorter transcripts this is fine; for very long recordings or batch analysis, it could be limiting.

### 5. Speed Advantage of Gemma 4 E4B

The one clear advantage: Gemma 4 E4B would be **significantly faster** on the RTX 3080. At ~2.5GB VRAM (Q4), it leaves 7.5GB free — enabling faster inference and larger batch sizes. Qwen 2.5 14B at ~10-12GB is tight on 10GB VRAM and runs slower. If speed matters more than quality for a particular use case, E4B could serve as a "quick summary" option alongside Qwen.

### 6. The Models That *Would* Beat Qwen Don't Fit

Gemma 4 31B Dense benchmarks are impressive:
- Arena AI (text): 1452
- MMLU: 85.2%
- GPQA Diamond: 84.3%
- LiveCodeBench: 80.0%

This would clearly outperform Qwen 2.5 14B — but it needs ~18GB VRAM at Q4. The 26B MoE needs ~15GB. Neither fits on 10GB.

### 7. Licensing Improvement

Gemma 4 ships under **Apache 2.0** (fully permissive), an upgrade from Gemma 3's restricted license. This removes any commercial-use concerns, matching Qwen's Apache 2.0 license.

## Implications for This Project

### Recommendation: Keep Qwen 2.5 14B as Default

For WhisperHeim's transcript analysis on the current hardware (RTX 3080 10GB):
- **Qwen 2.5 14B** remains the best choice for quality — better MMLU, better instruction following, larger context window
- **Gemma 4 E4B** could be offered as a lightweight/fast alternative, but expect noticeably worse extraction quality
- **Upgrade path**: If the user upgrades to a 16GB+ GPU (e.g., RTX 4070 Ti Super), Gemma 4 26B MoE becomes viable and would likely outperform Qwen 2.5 14B

### Possible Future Enhancement

Consider offering model selection in the Ollama settings: users with more VRAM could pick Gemma 4 26B MoE or 31B Dense for superior results, while keeping Qwen 2.5 14B as the recommended default for 10GB cards.

## Open Questions

- Would Qwen 3.5-9B (6GB VRAM, MMLU-Pro 82.5%) be a better upgrade than either Gemma option? It fits comfortably on 10GB and may outperform Qwen 2.5 14B on instruction following.
- Does Gemma 4 E4B's speed advantage justify offering it as a "quick recap" option alongside Qwen for full analysis?

## Sources

- [Google: Gemma 4 announcement](https://blog.google/innovation-and-ai/technology/developers-tools/gemma-4/)
- [Google DeepMind: Gemma 4](https://deepmind.google/models/gemma/gemma-4/)
- [Gemma 4 Hardware Guide — VRAM Requirements](https://www.compute-market.com/blog/gemma-4-local-hardware-guide-2026)
- [Gemma 3n E4B vs Qwen3.5-4B Comparison](https://llm-stats.com/models/compare/gemma-3n-e4b-it-vs-qwen3.5-4b)
- [Small Language Model Leaderboard](https://awesomeagents.ai/leaderboards/small-language-model-leaderboard/)
- [Home GPU LLM Leaderboard](https://awesomeagents.ai/leaderboards/home-gpu-llm-leaderboard/)
- [Ollama: Gemma 3n](https://ollama.com/library/gemma3n)
- [Gemma 4 vs Qwen 3.5 — Trending Topics](https://www.trendingtopics.eu/google-gemma-4-launch/)
- [Meetily: Local meeting notes with Ollama](https://dev.to/zackriya/local-meeting-notes-with-whisper-transcription-ollama-summaries-gemma3n-llama-mistral--2i3n)
