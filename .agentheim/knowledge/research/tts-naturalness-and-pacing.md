# Research: Pocket TTS Naturalness, Pacing, and Sentence Boundary Artifacts

**Date:** 2026-03-21
**Status:** Complete
**Relevance:** Milestone 4 (Text-to-Speech) -- improving voice quality and naturalness

## Summary

The "rushed" and "breaking voice between sentences" artifacts in WhisperHeim's TTS output stem from how sherpa-onnx's Pocket TTS implementation processes text: it splits input into sentences via `SplitByPunctuation()`, generates each sentence independently through `GenerateSingleSentence()`, and directly concatenates the audio with no explicit silence between segments. This sentence-by-sentence generation creates audible discontinuities at boundaries -- essentially each sentence *is* a new transcription, confirming the user's intuition.

There are several levers available to improve this, both through sherpa-onnx's `OfflineTtsGenerationConfig` parameters (which WhisperHeim currently doesn't fully utilize) and through application-level audio post-processing.

## Key Findings

### 1. Sherpa-onnx Generates Per-Sentence, Then Concatenates

The C++ implementation in `offline-tts-pocket-impl.h` reveals:

```
text → SplitByPunctuation() → MergeShortSentences(min=30 chars) → SplitLongSentence(max=200 chars)
     → for each sentence: GenerateSingleSentence() → concatenate samples directly
     → ScaleSilence(silence_scale) on final audio
```

**Key detail:** Audio from each sentence is appended with `samples.insert()` -- no silence gap is inserted between sentences. The `ScaleSilence()` call at the end only scales *existing* silent regions in the combined audio; it does not insert new ones.

### 2. Available Generation Parameters (via `Extra` Hashtable)

These parameters are extracted from `genConfig.Extra` in the native code but are **not currently set** by WhisperHeim:

| Parameter | Default | Effect |
|-----------|---------|--------|
| `temperature` | 0.7 | Controls sampling variance. Lower = more stable/predictable, higher = more expressive. Unlike token-based TTS, Pocket TTS adjusts Gaussian noise variance. |
| `max_frames` | 500 | Maximum audio frames per sentence. Limits generation length. |
| `frames_after_eos` | 3 | How many frames to continue generating after EOS is detected. **Higher values = more natural trailing silence per sentence.** |
| `chunk_size` | 15 | Internal generation chunk size. |
| `max_reference_audio_len` | 10 | Max seconds of reference audio used. WhisperHeim already sets this to 12. |
| `seed` | -1 | Random seed for reproducibility. -1 = random. |

### 3. `SilenceScale` Parameter

`OfflineTtsGenerationConfig.SilenceScale` (default 0.2) scales silent regions in the final concatenated audio. However:
- It only scales *existing* silence -- if sentences are concatenated with no gap, there's nothing to scale.
- GitHub issue #2043 reports this parameter doesn't work as expected for several TTS models.
- For Pocket TTS specifically, it calls `result.ScaleSilence(silence_scale)` after concatenation, which may have limited effect since silence at sentence boundaries is minimal.

### 4. `NumSteps` Controls Diffusion Quality

`NumSteps` (default 5 in C# config, maps to `num_steps` in native code) controls how many flow-matching diffusion iterations are used in `RunLmFlow()`. Each step refines the latent audio representation with step size `dt = 1.0 / num_steps`.

- **More steps = higher quality audio** but slower generation.
- The original Pocket TTS Python library defaults to `lsd_decode_steps=1` for maximum speed.
- Sherpa-onnx defaults to 5 steps for better quality.
- Increasing to 8-10 may improve smoothness and reduce artifacts, at the cost of generation speed.

### 5. Application-Level Solutions for Pacing

Since the native code doesn't insert inter-sentence silence, the most reliable approach is to handle it at the application level:

**Option A: Insert silence samples between sentences (recommended)**
- Split text into sentences before calling `GenerateWithConfig()`.
- Generate each sentence separately.
- Insert a configurable number of silence samples (e.g., 200-500ms of zeros at 24kHz = 4800-12000 samples) between each sentence's audio.
- This gives full control over pacing.

**Option B: Manipulate the input text**
- Add punctuation like `...` or `. .` between sentences to trick the model into generating longer pauses.
- Less predictable, model-dependent behavior.

**Option C: Post-process the concatenated audio**
- After generation, detect sentence boundaries (energy dips) and stretch/insert silence.
- More complex, but works without changing the generation pipeline.

**Option D: Increase `frames_after_eos`**
- Setting this higher (e.g., 10-20) via `genConfig.Extra["frames_after_eos"] = 10` adds trailing silence to each sentence the model generates.
- Simple to implement, but the silence duration isn't precisely controllable.

### 6. Voice Cloning Quality Factors

The "breaking voice" artifacts may also relate to:

- **Reference audio length**: Pocket TTS recommends 5-12 seconds. The 15-second sample the user trained on gets truncated to `max_reference_audio_len` (currently 12s). The truncation point matters -- if it cuts mid-word, it could affect quality.
- **Reference audio quality**: Pocket TTS reproduces the audio quality characteristics of the reference. Background noise, compression artifacts, or room reverb in the reference will be reproduced.
- **Short text artifacts**: Known issue in sherpa-onnx issue #3176 -- short phrases like "Hello World" produce "inaudible and weird sounds." Merging short sentences (min 30 chars) helps but doesn't eliminate this.
- **Int8 quantization**: WhisperHeim uses the int8 quantized model. This reduces quality slightly compared to the full-precision model. Voice cloning fidelity may suffer more from quantization than built-in voices.

## Implications for This Project

### Quick Wins (parameter tuning only)
1. Set `genConfig.Extra["frames_after_eos"] = 10` to add natural trailing silence per sentence.
2. Set `genConfig.Extra["temperature"] = 0.6f` (slightly lower) for more stable, less "glitchy" output.
3. Increase `genConfig.NumSteps` from 5 to 8 for smoother audio quality (test speed impact).
4. Increase `genConfig.SilenceScale` from 0.2 to 0.5-1.0 to expand any existing silence.

### Medium Effort (application-level sentence splitting with silence injection)
5. Split text into sentences in C# before calling the model.
6. Call `GenerateWithConfig()` per sentence.
7. Insert configurable silence (e.g., 300ms default, user-adjustable) between sentence audio buffers.
8. Add a "Sentence Pause" slider to the TTS settings UI (range: 0-1000ms).

### Worth Testing
9. Try the full-precision (non-int8) Pocket TTS model to see if voice cloning artifacts improve.
10. Experiment with reference audio: try a 10-second clean clip vs the current 15-second clip (which gets truncated anyway).

## Open Questions
- Does increasing `NumSteps` to 8-10 noticeably reduce voice artifacts on the int8 model?
- Would the full-precision Pocket TTS model be acceptably fast on the user's hardware?
- Is `SilenceScale` actually functional for Pocket TTS in the current sherpa-onnx version?
- Would using the built-in voices (bria/loona) show the same sentence boundary artifacts, or is it specific to voice cloning?

## Sources
- [Pocket TTS GitHub repo](https://github.com/kyutai-labs/pocket-tts)
- [Pocket TTS Python API docs](https://kyutai-labs.github.io/pocket-tts/API%20Reference/python-api/)
- [Pocket TTS blog post](https://kyutai.org/blog/2026-01-13-pocket-tts)
- [sherpa-onnx OfflineTtsGenerationConfig C# source](https://github.com/k2-fsa/sherpa-onnx/blob/master/scripts/dotnet/OfflineTtsGenerationConfig.cs)
- [sherpa-onnx Pocket TTS C++ implementation](https://github.com/k2-fsa/sherpa-onnx/blob/master/sherpa-onnx/csrc/offline-tts-pocket-impl.h)
- [sherpa-onnx issue #2043 - SilenceScale not working](https://github.com/k2-fsa/sherpa-onnx/issues/2043)
- [sherpa-onnx issue #3176 - Pocket TTS short text artifacts](https://github.com/k2-fsa/sherpa-onnx/issues/3176)
- [sherpa-onnx offline-tts-play CLI source](https://github.com/k2-fsa/sherpa-onnx/blob/master/sherpa-onnx/csrc/sherpa-onnx-offline-tts-play.cc)
