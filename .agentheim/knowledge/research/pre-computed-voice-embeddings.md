# Research: Pre-Computed Voice Embeddings for Faster TTS

**Date:** 2026-03-31
**Status:** Complete
**Relevance:** Milestone 1 / TTS performance — eliminating per-session voice encoding overhead

## Summary

Pocket TTS (by Kyutai) supports two modes for voice cloning: (1) pass raw WAV audio, which runs through the Mimi encoder on every call (~slow), or (2) load a pre-computed `.safetensors` file containing the KV cache from a previous encoding (~instant). The Python `pocket-tts` library fully supports both modes, including an `export-voice` CLI and `export_model_state()` API for converting WAV → safetensors.

However, **sherpa-onnx does not support loading safetensors embeddings**. Its C/C++/C# API only accepts raw audio via `ReferenceAudio` (float[]) in `OfflineTtsGenerationConfig`. It has an in-memory `VoiceEmbeddingCacheCapacity` (default 50) that caches encoder output during a session, but this cache is not persisted to disk and is lost on restart.

WhisperHeim currently mitigates this with a warm-up step (generates dummy "ready" text for the default voice at startup, populating the in-memory cache). But only 1 voice is warmed up, and the cache resets every app restart.

## Key Findings

### 1. Pocket TTS Python Has Exactly What We Need

The official Python library provides:
```python
model_state = model.get_state_for_audio_prompt("voice.wav")  # slow: runs encoder
export_model_state(model_state, "voice.safetensors")          # save to disk

# Later (instant):
model_state = model.get_state_for_audio_prompt("voice.safetensors")  # fast: just reads KV cache
```

The `.safetensors` file contains the full KV cache (~1-2 MB per voice), not just a speaker embedding vector. Loading it bypasses the Mimi encoder entirely.

The `export-voice` CLI command wraps this for batch use.

### 2. Sherpa-onnx Does NOT Support Pre-Computed Embeddings

The `OfflineTtsPocketModelConfig` struct has no fields for safetensors/embedding paths. The C# `OfflineTtsGenerationConfig` only accepts:
- `ReferenceAudio` (float[] raw samples)
- `ReferenceSampleRate` (int)

There is no API to pass pre-computed encoder output or KV cache state. The in-memory `VoiceEmbeddingCacheCapacity` is the only optimization available.

### 3. The Encoder Is a Separate ONNX File

The Pocket TTS model ships with `encoder.onnx` (the Mimi encoder). In theory, one could:
1. Run `encoder.onnx` directly via ONNX Runtime in C#
2. Cache the output tensor to disk
3. Inject it into the generation pipeline

But sherpa-onnx's internal pipeline doesn't expose a hook to skip the encoder step and inject cached state. This would require modifying sherpa-onnx's C++ source.

## Options Analysis

### Option A: Warm Up All Voices at Startup (Immediate, No Dependencies)

**Effort:** ~30 min code change
**Impact:** Eliminates first-use delay within a session; no help across restarts

Extend `WarmUpAsync` to iterate all voices (or a user-configured "favorites" list) and run a dummy generation for each, populating the in-memory embedding cache. The cache (capacity 10-50) is large enough for typical usage.

**Pros:** Zero new dependencies, works today
**Cons:** Adds 3-8 seconds to startup per voice; cache lost on restart; doesn't scale to many voices

### Option B: Python Sidecar Using pocket-tts (Short-Term, Full Speed)

**Effort:** 1-2 days
**Impact:** Full pre-computed embedding speed; instant voice loading

Replace sherpa-onnx TTS with a small Python process running the `pocket-tts` library:
1. One-time: `pocket-tts export-voice voice.wav -o voice.safetensors` for each custom voice
2. At runtime: Python sidecar loads model + safetensors, receives text via stdin/pipe, returns PCM audio
3. C# side sends text, receives float[] audio, plays via NAudio as before

**Pros:** Full safetensors support today; matches the "original Python version" behavior exactly
**Cons:** Adds Python dependency (~150 MB); process management complexity; departure from current "pure .NET" architecture

### Option C: Contribute Safetensors Support to Sherpa-onnx (Right Fix, Takes Time)

**Effort:** 2-5 days for the PR + review cycle
**Impact:** Native C# support for pre-computed embeddings

Submit a PR to sherpa-onnx that:
1. Adds an optional `pre_computed_embedding` field to `OfflineTtsPocketModelConfig` or `OfflineTtsGenerationConfig`
2. When set, skips the Mimi encoder and uses the cached KV state directly
3. Optionally adds an API to export the computed embedding after first generation

This is what was discussed previously ("push it upstream"). The sherpa-onnx maintainer (csukuangfj) is responsive but the review cycle adds latency.

**Pros:** Clean long-term solution; benefits entire sherpa-onnx community
**Cons:** Depends on upstream acceptance; PR review takes days-weeks

### Option D: Fork Sherpa-onnx Locally (Fastest Native Fix)

**Effort:** 1-2 days
**Impact:** Native C# support for pre-computed embeddings, no upstream dependency

Fork sherpa-onnx, add the safetensors/embedding bypass to the C++ code, build a custom NuGet. Use the Python `pocket-tts` CLI to pre-export voices. Later merge upstream when/if the PR is accepted.

**Pros:** Full speed, stays in .NET, no Python at runtime
**Cons:** Maintenance burden of a fork; need to build native binaries

## Recommended Path

**Phase 1 (today):** Warm up all voices at startup (Option A). This is a trivial code change that eliminates the per-session first-use delay.

**Phase 2 (this week):** Use Python `pocket-tts` CLI to pre-export all custom voices to `.safetensors` files. This is a one-time offline step per voice — no Python dependency at runtime.

**Phase 3 (submit PR):** Contribute a PR to sherpa-onnx to accept pre-computed embeddings via the GenerationConfig. Reference the safetensors files from Phase 2. Once merged and released, WhisperHeim loads safetensors natively in C#. No Python sidecar needed.

If the upstream PR stalls, fall back to Option D (local fork).

## Open Questions

- What is the exact tensor shape/format of the KV cache in the safetensors file? Needed for the sherpa-onnx PR.
- Does the sherpa-onnx maintainer have this on their radar already? Worth checking issues/discussions before filing.
- Would a simpler approach work: just expose an API to save/load the in-memory embedding cache to disk?

## Sources

- [Kyutai Pocket TTS Documentation](https://kyutai-labs.github.io/pocket-tts/)
- [Pocket TTS GitHub (kyutai-labs)](https://github.com/kyutai-labs/pocket-tts)
- [Predefined Voices - DeepWiki](https://deepwiki.com/kyutai-labs/pocket-tts/7.2-predefined-voices)
- [kyutai/tts-voices on HuggingFace](https://huggingface.co/kyutai/tts-voices)
- [sherpa-onnx GitHub](https://github.com/k2-fsa/sherpa-onnx)
- [sherpa-onnx Pocket TTS Issue #3176](https://github.com/k2-fsa/sherpa-onnx/issues/3176)
- [Pocket TTS Technical Report](https://kyutai.org/pocket-tts-technical-report)
