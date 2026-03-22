# Research: Pocket TTS Voice Embedding Pre-computation

**Date:** 2026-03-22
**Status:** Complete

## Summary

Pre-computing and storing voice embeddings to disk **is a core feature of the original Kyutai Pocket TTS** library. However, **sherpa-onnx does NOT currently expose this capability** through its API. Sherpa-onnx only provides an in-memory LRU cache for voice embeddings, with no disk persistence.

Three options were evaluated for pre-computing voice embeddings in WhisperHeim:

| Option | Feasibility | Effort | Benefit |
|--------|-------------|--------|---------|
| A: Kyutai Python lib | Medium | Low (offline tool) | Pre-compute .safetensors, but can't load them into sherpa-onnx |
| B: Run encoder.onnx directly | Medium-High | High | Could extract embeddings, but can't feed them back to sherpa-onnx |
| C: Contribute to sherpa-onnx | High | Medium | Would solve the problem at the root; project is open to PRs |

**Bottom line:** The in-memory LRU cache in sherpa-onnx already provides the main benefit (encoder runs once per voice per session). The first-call cost (~1-3s) is the only remaining overhead. Contributing a feature to sherpa-onnx (Option C) is the most viable path to true disk-based pre-computation.

---

## Option A: Use the Kyutai Pocket TTS Library Directly

### What Is It?

The original Kyutai `pocket-tts` library is a **Python package** (89.1% Python, with some Rust extensions for training). It has no standalone binary or Rust library suitable for FFI.

- **Language:** Python
- **Install:** `pip install pocket-tts`
- **Dependencies:** PyTorch 2.5+ (CPU-only works), scipy
- **License:** Not explicitly stated in README (check repo)

### CLI Tool: `export-voice`

Kyutai provides a CLI command to pre-compute voice embeddings:

```bash
pocket-tts export-voice --audio-path ./voice.wav --export-path ./voice.safetensors
```

This converts a reference WAV file into a `.safetensors` file containing the pre-computed KV cache state.

### Python API

```python
from pocket_tts import TTSModel, export_model_state

model = TTSModel.load_model()
model_state = model.get_state_for_audio_prompt("some_voice.wav")
export_model_state(model_state, "./some_voice.safetensors")
# Later: load from disk (instant, no encoder inference)
model_state = model.get_state_for_audio_prompt("./some_voice.safetensors")
```

### What Gets Saved in the .safetensors File

**Two levels of voice state exist:**

1. **Simple predefined voices:** A single tensor with key `"audio_prompt"` containing the Mimi encoder output (dimension 1024, shape `[1, num_frames, 1024]`).

2. **Full exported voice states** (from `export_model_state`): A flattened dictionary containing:
   - Per-module KV cache tensors with shape `[2, B, T, H, D]` (key-value pairs for each attention layer)
   - Convolution buffers for causal processing
   - Offset tracking tensors for positional information
   - Keys are in `"module_name/key"` format

The full state is much richer than just the encoder output -- it includes the FlowLM's initialized KV cache after processing the audio prompt through the language model.

### Can It Be Called from C#/.NET?

- **No native C#/.NET interop.** It's a Python package requiring PyTorch.
- **Shell out to CLI:** Could run `pocket-tts export-voice` as a subprocess. Requires Python + PyTorch installed (~2-4GB), which is impractical to bundle with a desktop app.
- **Offline pre-computation:** Could be used as a developer tool to pre-compute voices at build time, but the resulting `.safetensors` files cannot currently be loaded by sherpa-onnx.

### Verdict: Not Useful for Runtime, Limited Offline Utility

The Kyutai library can pre-compute embeddings, but sherpa-onnx has no API to accept them. Pre-computed `.safetensors` files would sit unused unless sherpa-onnx adds loading support. The PyTorch dependency makes it impractical for end-user machines.

---

## Option B: Run encoder.onnx Directly with ONNX Runtime in C#

### The Encoder Model

The Pocket TTS model directory contains `encoder.onnx` (the Mimi encoder). Sherpa-onnx's C++ code reveals:

- **Input:** Audio tensor with shape `(1, 1, num_samples)` at 24kHz float32
- **Output:** Float32 tensor with shape `(1, num_frames, 1024)`
- **Processing:** Audio is resampled to 24kHz, truncated to max 10 seconds

From the sherpa-onnx header (`offline-tts-pocket-model.h`):
```cpp
// RunMimiEncoder: audio input shape (1, 1, num_samples) at 24kHz
// Returns float32 tensor of shape (1, num_frames, 1024)
Ort::Value RunMimiEncoder(Ort::Value audio);
```

### Could We Run It in C#?

Yes, technically. Using `Microsoft.ML.OnnxRuntime` NuGet package:

```csharp
using var session = new InferenceSession("encoder.onnx");
var audioTensor = new DenseTensor<float>(audioData, new[] { 1, 1, numSamples });
var inputs = new List<NamedOnnxValue> {
    NamedOnnxValue.CreateFromTensor("audio", audioTensor)
};
using var results = session.Run(inputs);
var embedding = results[0].AsTensor<float>(); // (1, num_frames, 1024)
```

### The Critical Problem: No Way to Feed It Back

Even if we extract the Mimi encoder output, **sherpa-onnx's generation API does not accept pre-computed embeddings.** The `SherpaOnnxGenerationConfig` only takes raw audio:

```c
typedef struct SherpaOnnxGenerationConfig {
  const float *reference_audio;      // raw PCM samples only
  int32_t reference_audio_len;
  int32_t reference_sample_rate;
  // ... no field for pre-computed embeddings
} SherpaOnnxGenerationConfig;
```

The pipeline inside sherpa-onnx is:
1. `RunMimiEncoder(audio)` -> embedding `(1, num_frames, 1024)`
2. `speaker_proj_weight` projection: 1024 -> 512
3. Feed into `RunLmMain()` to initialize KV cache
4. Iterative `RunLmFlow()` + `RunLmMain()` generation loop
5. `RunMimiDecoder()` for audio output

We could replicate step 1, but steps 2-5 are deeply integrated into sherpa-onnx's C++ code and NOT exposed as individual callable functions through the C API.

### What Format Would We Save In?

If we could extract and save:
- **Raw binary:** `float[]` dump of the `(1, num_frames, 1024)` tensor
- **Safetensors:** Compatible with Kyutai's format (key: `"audio_prompt"`)
- **NumPy .npy:** Simple format readable by many tools

But without a way to inject these back into sherpa-onnx, the format is moot.

### Verdict: Technically Possible but Useless Without sherpa-onnx API Changes

Running `encoder.onnx` directly is straightforward with ONNX Runtime in C#. But the result cannot be fed back into sherpa-onnx's generation pipeline. This approach would only become viable if sherpa-onnx added an API to accept pre-computed Mimi encoder output.

---

## Option C: Contribute to sherpa-onnx

### Is sherpa-onnx Open to Contributions?

- **License:** Apache 2.0 (permissive, contribution-friendly)
- **Activity:** 1,784+ commits, 1,164+ contributors, 39 open PRs
- **Community:** Active Discord community
- **Languages:** C/C++ core with bindings for 12 languages (C#, Python, Go, JS, etc.)
- **No formal CONTRIBUTING.md** found, but the project clearly accepts external PRs (many community-contributed features visible in the changelog)

### What Would Need to Be Added?

**Minimum viable feature: Accept pre-computed Mimi encoder output**

1. **C API change** -- Add optional fields to `SherpaOnnxGenerationConfig`:
   ```c
   const float *voice_embedding;     // pre-computed encoder output
   int32_t voice_embedding_frames;   // number of frames
   int32_t voice_embedding_dim;      // 1024
   ```

2. **C++ implementation** -- Modify `offline-tts-pocket-impl.h`'s `GetVoiceEmbedding()` to check for pre-computed data before calling `RunMimiEncoder()`.

3. **C# binding** -- Add corresponding properties to `OfflineTtsGenerationConfig`.

**More ambitious: Add extraction + save/load API**

1. Add `SherpaOnnxOfflineTtsExtractVoiceEmbedding()` function to the C API
2. Add `SherpaOnnxOfflineTtsLoadVoiceEmbedding()` function
3. Support loading `.safetensors` format (or a simpler binary format)

### How Hard Would This Be?

**Medium difficulty.** The codebase is well-structured:

- `offline-tts-pocket-impl.h` -- Contains `GetVoiceEmbedding()` with the LRU cache, which is the natural insertion point
- `offline-tts-pocket-model.h` / `.cc` -- Contains `RunMimiEncoder()` which is cleanly separated
- `c-api.h` / `c-api.cc` -- The C API layer would need new fields/functions
- `scripts/dotnet/` -- C# bindings follow a mechanical pattern matching the C API

The voice embedding cache (`VoiceEmbeddingCache`) already stores `Ort::Value` tensors keyed by audio hash. Adding a "load from file" path is conceptually straightforward.

### Alternative: Feature Request

Filing a GitHub issue requesting this feature might get it implemented by the maintainers, given that:
- The in-memory cache already exists (proves the value)
- Kyutai's Python library already supports this workflow
- The use case (faster first-call voice loading) is universal

### Verdict: Most Viable Long-term Path

Contributing to sherpa-onnx or filing a feature request is the proper solution. The codebase is approachable, the project accepts contributions, and the feature would benefit all sherpa-onnx users.

---

## What sherpa-onnx Provides Today (Existing Behavior)

### In-Memory LRU Cache

Sherpa-onnx already has a thread-safe in-memory LRU cache for voice embeddings:

- **Mechanism:** Hashes the reference audio data and caches the Mimi encoder output
- **Config field:** `voice_embedding_cache_capacity` (default: 50, 0 disables)
- **C# property:** `OfflineTtsPocketModelConfig.VoiceEmbeddingCacheCapacity`
- **Cache key:** Hash of audio sample data
- **Eviction:** LRU when capacity exceeded
- **Thread safety:** Mutex-protected
- **Disk persistence:** NONE

### How the Cache Helps WhisperHeim

When WhisperHeim generates speech for the same voice across multiple text chunks (our `SplitTextIntoChunks` approach), the Mimi encoder only runs on the **first chunk**. All subsequent chunks hit the in-memory cache. This is already a significant optimization.

The remaining cost is the **first call per voice per application session** (~1-3 seconds for encoder inference).

### C# Example (pocket-tts-zero-shot)

sherpa-onnx provides a working C# example at `dotnet-examples/pocket-tts-zero-shot/Program.cs`:

```csharp
var config = new OfflineTtsConfig();
config.Model.Pocket.LmFlow = "lm_flow.int8.onnx";
config.Model.Pocket.LmMain = "lm_main.int8.onnx";
config.Model.Pocket.Encoder = "encoder.onnx";
config.Model.Pocket.Decoder = "decoder.int8.onnx";
config.Model.Pocket.TextConditioner = "text_conditioner.onnx";
config.Model.Pocket.VocabJson = "vocab.json";
config.Model.Pocket.TokenScoresJson = "token_scores.json";

var genConfig = new OfflineTtsGenerationConfig();
genConfig.ReferenceAudio = reader.Samples;
genConfig.ReferenceSampleRate = reader.SampleRate;
genConfig.Extra["max_reference_audio_len"] = 12;
```

---

## Immediate Actionable Item

**Enable the in-memory voice embedding cache** in our `TextToSpeechService.LoadModel()`:

```csharp
config.Model.Pocket.VoiceEmbeddingCacheCapacity = 10;
```

This costs nothing and ensures that when we generate multiple chunks for the same voice, the encoder only runs once per session. Our current code does NOT set this property.

## Recommended Next Steps

1. **Immediate:** Add `VoiceEmbeddingCacheCapacity = 10` to `TextToSpeechService.LoadModel()`.
2. **Short-term:** File a feature request on sherpa-onnx GitHub for pre-computed embedding loading support.
3. **Medium-term:** If the feature request gets traction, contribute a PR or wait for implementation.
4. **Long-term alternative:** If sherpa-onnx support never materializes, the Kyutai Python CLI could be used as an offline developer tool to pre-compute voices, then a custom integration path would need to be built.

---

## Technical Reference: Pocket TTS Architecture

### Model Components (ONNX files)

| File | Purpose | Input | Output |
|------|---------|-------|--------|
| `encoder.onnx` | Mimi encoder (voice embedding) | `(1, 1, num_samples)` float32 @ 24kHz | `(1, num_frames, 1024)` float32 |
| `text_conditioner.onnx` | Text tokenization + embedding | `(1, num_tokens)` int64 | `(1, num_tokens, 1024)` float32 |
| `lm_main.onnx` / `lm_main.int8.onnx` | Main language model (stateful) | Embeddings + KV cache state | Updated state + latents |
| `lm_flow.onnx` / `lm_flow.int8.onnx` | Flow-matching ODE solver | Conditioning + noise | Denoised latents |
| `decoder.onnx` / `decoder.int8.onnx` | Mimi decoder (latents -> audio) | Latent frames | Audio samples @ 24kHz |

### Voice Embedding Pipeline

```
Reference WAV (any sample rate)
    |
    v
Resample to 24kHz mono, truncate to 10s max
    |
    v
Mimi Encoder: (1, 1, num_samples) -> (1, num_frames, 1024)
    |
    v
speaker_proj_weight: 1024 -> 512 linear projection
    |
    v
Feed into LM Main to initialize KV cache
    |
    v
Voice state ready for generation
```

### Safetensors Voice State Format (Kyutai)

**Simple format** (predefined voices):
- Single tensor key: `"audio_prompt"` -> shape `(1, num_frames, 1024)`

**Full format** (exported via `export_model_state`):
- Flattened dictionary with `"module_name/key"` format keys
- Contains: KV cache tensors `[2, B, T, H, D]`, convolution buffers, offset tensors
- Much larger than simple format (includes full LM state)

---

## Sources

- [Kyutai Pocket TTS GitHub](https://github.com/kyutai-labs/pocket-tts)
- [Kyutai Pocket TTS Python API docs](https://kyutai-labs.github.io/pocket-tts/API%20Reference/python-api/)
- [Kyutai Pocket TTS README](https://github.com/kyutai-labs/pocket-tts/blob/main/README.md)
- [Predefined Voices deep dive (DeepWiki)](https://deepwiki.com/kyutai-labs/pocket-tts/7.2-predefined-voices)
- [Pocket TTS architecture (DeepWiki)](https://deepwiki.com/kyutai-labs/pocket-tts)
- [sherpa-onnx GitHub](https://github.com/k2-fsa/sherpa-onnx)
- [sherpa-onnx C API header](https://github.com/k2-fsa/sherpa-onnx/blob/master/sherpa-onnx/c-api/c-api.h)
- [sherpa-onnx Pocket TTS C++ impl](https://github.com/k2-fsa/sherpa-onnx/blob/master/sherpa-onnx/csrc/offline-tts-pocket-impl.h)
- [sherpa-onnx Pocket TTS model header](https://github.com/k2-fsa/sherpa-onnx/blob/master/sherpa-onnx/csrc/offline-tts-pocket-model.h)
- [sherpa-onnx C# Pocket config](https://github.com/k2-fsa/sherpa-onnx/blob/master/scripts/dotnet/OfflineTtsPocketModelConfig.cs)
- [sherpa-onnx C# example (pocket-tts-zero-shot)](https://github.com/k2-fsa/sherpa-onnx/tree/master/dotnet-examples/pocket-tts-zero-shot)
- [sherpa-onnx License (Apache 2.0)](https://github.com/k2-fsa/sherpa-onnx/blob/master/LICENSE)
- [Pocket TTS ONNX export scripts](https://github.com/KevinAHM/pocket-tts-onnx-export)
- [sherpa-onnx Issue #3176: How to use Pocket TTS](https://github.com/k2-fsa/sherpa-onnx/issues/3176)
- [PyPI: pocket-tts](https://pypi.org/project/pocket-tts/)
- [NuGet: org.k2fsa.sherpa.onnx](https://www.nuget.org/packages/org.k2fsa.sherpa.onnx)
