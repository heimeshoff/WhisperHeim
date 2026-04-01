# Research: TTS Voice Cloning Models -- State of the Art (April 2026)

**Date:** 2026-04-01
**Status:** Complete
**Relevance:** Milestone 4 (Text-to-Speech) -- evaluating whether to replace or supplement Pocket TTS

## Summary

The "French AI company" that released a new TTS model is **Mistral AI**, who launched **Voxtral TTS** (4B params) on March 23, 2026. It has excellent zero-shot voice cloning (68.4% win rate vs ElevenLabs) and supports 9 languages including German. However, its **CC BY-NC 4.0 license blocks commercial use**, it requires ~16GB VRAM at full precision, and it has **no ONNX export or sherpa-onnx integration**. It is not viable for WhisperHeim today.

The broader landscape has evolved significantly since Pocket TTS was chosen. The most promising upgrade candidates are **Chatterbox Turbo** (Resemble AI, MIT license, 23 languages including German, ONNX available) and **Qwen3-TTS 0.6B** (Alibaba, Apache 2.0, 10 languages including German, community ONNX export). Within sherpa-onnx specifically, **ZipVoice** is a new first-party voice cloning model that would be a trivial integration swap. Pocket TTS remains the best currently-integrated option, but its lack of German is a real gap.

## Key Findings

### 1. Voxtral TTS (Mistral AI) -- The "French AI Company" Release

Released March 23, 2026. Mistral's first TTS model.

| Attribute | Details |
|---|---|
| **Model** | Voxtral-4B-TTS-2603 |
| **Size** | 4B params (3.4B backbone + 390M flow-matching + 300M codec) |
| **Voice cloning** | Zero-shot from 3-5s audio; 68.4% win rate vs ElevenLabs |
| **Languages** | 9: EN, FR, **DE**, ES, NL, PT, IT, HI, AR |
| **Speed** | 70ms time-to-first-audio, RTF ~9.7x |
| **VRAM** | ~16GB BF16, ~3-4GB quantized |
| **License** | **CC BY-NC 4.0 -- NO commercial use** (API only for commercial) |
| **ONNX** | No export available |
| **sherpa-onnx** | No support |

**Verdict:** Blocked by license and integration. Excellent quality and German support, but cannot be used commercially and has no path into our sherpa-onnx pipeline. Worth monitoring if license changes.

Sources: [Mistral announcement](https://mistral.ai/news/voxtral-tts), [HuggingFace](https://huggingface.co/mistralai/Voxtral-4B-TTS-2603)

### 2. Chatterbox Turbo (Resemble AI) -- Best Upgrade Candidate

| Attribute | Details |
|---|---|
| **Size** | 350M params (Turbo variant) |
| **Voice cloning** | Zero-shot from ~10s reference |
| **Languages** | **23 languages including German** |
| **License** | **MIT** -- commercial use OK |
| **ONNX** | YES (`ResembleAI/chatterbox-turbo-ONNX` on HuggingFace) |
| **sherpa-onnx** | No native support -- would need custom integration or standalone ONNX Runtime |
| **Speed** | GPU recommended, but 350M is manageable |

**Verdict:** The strongest candidate to replace or supplement Pocket TTS. MIT license, German support, voice cloning, and ONNX available. The main cost is integration: either a new sherpa-onnx backend (C++ PR), a standalone ONNX Runtime wrapper in C#, or a Python sidecar.

Sources: [GitHub](https://github.com/resemble-ai/chatterbox), [ONNX model](https://huggingface.co/ResembleAI/chatterbox-turbo-ONNX), [Multilingual announcement](https://www.resemble.ai/introducing-chatterbox-multilingual-open-source-tts-for-23-languages/)

### 3. Qwen3-TTS 0.6B (Alibaba) -- Strong Runner-Up

| Attribute | Details |
|---|---|
| **Size** | 0.6B and 1.7B variants |
| **Voice cloning** | Yes, 3-second rapid clone |
| **Languages** | **10: ZH, EN, JA, KO, DE, FR, RU, PT, ES, IT** |
| **License** | **Apache 2.0** -- commercial use OK |
| **ONNX** | Community export (`zukky/Qwen3-TTS-ONNX-DLL`) |
| **sherpa-onnx** | No native support |
| **Quality** | 1.835% WER across 10 langs, 0.789 speaker similarity |

**Verdict:** Excellent benchmarks, German support, permissive license. The 0.6B variant might run on CPU. Community ONNX export exists but maturity is uncertain.

Sources: [GitHub](https://github.com/QwenLM/Qwen3-TTS), [Blog](https://qwen.ai/blog?id=qwen3tts-0115), [ONNX DLL](https://huggingface.co/zukky/Qwen3-TTS-ONNX-DLL)

### 4. ZipVoice (k2-fsa) -- Easiest sherpa-onnx Upgrade

| Attribute | Details |
|---|---|
| **Size** | 123M params, int8 distill ~104MB |
| **Voice cloning** | Zero-shot from < 3s reference + transcript |
| **Languages** | Chinese + English only (no German) |
| **License** | Open source (k2-fsa) |
| **sherpa-onnx** | **YES -- native support, C# example exists** |
| **Speed** | 4 inference steps (distill), CPU-capable |

**Verdict:** Trivial to integrate (same API, same NuGet, C# example ready), but **no German**. Could serve as a quality comparison against Pocket TTS for English voice cloning.

Sources: [GitHub](https://github.com/k2-fsa/ZipVoice), [PR #2487](https://github.com/k2-fsa/sherpa-onnx/pull/2487)

### 5. Other Notable Models

| Model | Voice Clone | German | License | ONNX | sherpa-onnx | Notes |
|---|---|---|---|---|---|---|
| **Kokoro v1.1** (82M) | No | No | Apache 2.0 | Yes | Yes | 103 preset voices, no cloning |
| **F5-TTS** | Yes | Yes (cross-lingual) | MIT | Yes (community) | No | Good quality, needs custom integration |
| **Sesame CSM** (1B) | Community | No (EN only) | Apache 2.0 | No | No | Very natural conversational speech |
| **CosyVoice 2/3** | Yes | No (ZH/EN focus) | Open source | Partial | No | Multi-component, hard to ONNX-ify |
| **Fish Speech S2** | Yes | Yes | Apache 2.0 | No | No | Cloud-focused, local deployment unclear |
| **XTTS v2** (Coqui) | Yes | Yes | MPL 2.0 | No | No | Abandoned (Coqui bankrupt late 2024) |
| **Supertonic** (66M) | Yes (Voice Builder) | No | Open source | Yes | Yes | New in sherpa-onnx, but no German |

### 6. Pocket TTS Status (Current Model)

- Kyutai has NOT released anything newer than Pocket TTS (Jan 2026).
- **German is officially planned** but not yet available. Tracked at [GitHub issue #118](https://github.com/kyutai-labs/pocket-tts/issues/118). Languages in queue: DE, ES, FR, PT, IT.
- Kyutai TTS 1.6B exists as a larger server model (used in their Unmute product), separate from the local Pocket TTS.

## Comparison Matrix: WhisperHeim Requirements

| Requirement | Pocket TTS | Voxtral | Chatterbox | Qwen3-TTS | ZipVoice | F5-TTS |
|---|---|---|---|---|---|---|
| Voice cloning | YES | YES | YES | YES | YES | YES |
| German | Planned | YES | YES | YES | No | Yes (cross-lingual) |
| English | YES | YES | YES | YES | YES | YES |
| Commercial license | MIT | **NO (NC)** | MIT | Apache 2.0 | Open | MIT |
| ONNX available | YES | No | YES | Community | YES | Community |
| sherpa-onnx native | YES | No | No | No | YES | No |
| CPU-capable | YES | No | GPU rec. | 0.6B maybe | YES | GPU rec. |
| Model size | 100M | 4B | 350M | 600M | 123M | ~300M |

## Implications for This Project

### Recommended Strategy

**Keep Pocket TTS as primary** -- it is integrated, MIT-licensed, CPU-capable, and German is coming. No other model matches its integration maturity in sherpa-onnx.

**Evaluate Chatterbox Turbo as upgrade path** when German becomes a hard requirement:
1. Download `ResembleAI/chatterbox-turbo-ONNX` from HuggingFace
2. Test voice cloning quality in a standalone C# ONNX Runtime prototype
3. Compare output quality against Pocket TTS for English
4. If quality is clearly better, integrate via direct ONNX Runtime (bypassing sherpa-onnx) or submit a sherpa-onnx PR

**Test ZipVoice** as a low-effort experiment:
- Already has C# example in sherpa-onnx
- < 3s reference audio (shorter than Pocket TTS)
- Compare cloning quality against Pocket TTS for English

**Monitor Pocket TTS German release** -- the Kyutai team confirmed it's planned. Once available, it may remain the best overall choice.

### Integration Paths (ranked by effort)

1. **ZipVoice** -- swap config in existing sherpa-onnx setup (~1 hour)
2. **Chatterbox via ONNX Runtime** -- new C# wrapper, bypass sherpa-onnx (~2-3 days)
3. **Qwen3-TTS via community ONNX** -- similar to Chatterbox path (~2-3 days, less proven)
4. **F5-TTS via ONNX Runtime** -- community ONNX export, custom pipeline (~3-5 days)
5. **Contribute Chatterbox backend to sherpa-onnx** -- C++ work + PR cycle (~1-2 weeks)

## Open Questions

- How does Chatterbox Turbo voice cloning quality compare to Pocket TTS subjectively? Need a side-by-side test.
- When will Kyutai release German for Pocket TTS? No timeline given in the GitHub issue.
- Is the Qwen3-TTS community ONNX export production-quality or experimental?
- Could Voxtral's license change? Mistral has historically started restrictive and loosened later.
- Does ZipVoice's 3-second reference audio produce comparable clone quality to Pocket TTS's 10-12s reference?

## Sources

- [Mistral Voxtral TTS announcement](https://mistral.ai/news/voxtral-tts)
- [Voxtral on HuggingFace](https://huggingface.co/mistralai/Voxtral-4B-TTS-2603)
- [Chatterbox GitHub](https://github.com/resemble-ai/chatterbox)
- [Chatterbox Turbo ONNX](https://huggingface.co/ResembleAI/chatterbox-turbo-ONNX)
- [Chatterbox Multilingual](https://www.resemble.ai/introducing-chatterbox-multilingual-open-source-tts-for-23-languages/)
- [Qwen3-TTS GitHub](https://github.com/QwenLM/Qwen3-TTS)
- [Qwen3-TTS ONNX DLL](https://huggingface.co/zukky/Qwen3-TTS-ONNX-DLL)
- [ZipVoice GitHub](https://github.com/k2-fsa/ZipVoice)
- [sherpa-onnx ZipVoice PR #2487](https://github.com/k2-fsa/sherpa-onnx/pull/2487)
- [Kyutai Pocket TTS](https://github.com/kyutai-labs/pocket-tts)
- [Pocket TTS German issue #118](https://github.com/kyutai-labs/pocket-tts/issues/118)
- [Kokoro ONNX](https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX)
- [Sesame CSM](https://github.com/SesameAILabs/csm)
- [F5-TTS ONNX](https://github.com/DakeQQ/F5-TTS-ONNX)
- [F5-TTS voice cloning ONNX](https://github.com/nsarang/voice-cloning-f5-tts)
- [CosyVoice](https://github.com/FunAudioLLM/CosyVoice)
- [Fish Speech](https://github.com/fishaudio/fish-speech)
- [Supertonic](https://github.com/supertone-inc/supertonic)
- [sherpa-onnx TTS models](https://k2-fsa.github.io/sherpa/onnx/tts/all/)
- [BentoML TTS comparison](https://bentoml.com/blog/exploring-the-world-of-open-source-text-to-speech-models)
