# Research Index

## ASR Models (2026-03-21)

- **NVIDIA Parakeet TDT 0.6B V3**: Best local model for EN/DE. 6.32% WER, 3333x RTF, ~2-3GB VRAM, no silence hallucinations. 25 European languages.
- **Whisper Large V3 Turbo**: Best multilingual fallback. 0.8B params, 7.75% WER, 99+ languages. Use via faster-whisper for 4x speedup.
- **Distil-Whisper Large V3**: 6x faster than Whisper LV3, within 1% WER. English only.

## Streaming Approaches (2026-03-21)

- **VAD + chunked transcription**: Silero VAD for speech detection, send speech segments to ASR. ~380-520ms latency. Used by WhisperLive, whisper.cpp.
- **Tumbling window (WhisperFlow)**: Accumulate audio, transcribe every cycle, emit when stable. ~275ms latency.
- **LocalAgreement (whisper_streaming)**: Confirm prefix only if N consecutive iterations agree. ~3.3s latency.
- **AlignAtt (SimulStreaming)**: Inspect attention weights to know when to stop decoding. 5x faster than LocalAgreement.

## Speaker Diarization (2026-03-21)

- **sherpa-onnx (NuGet)**: Native .NET, pyannote segmentation 3.0 ONNX, no Python needed. CPU-capable. Best for .NET integration.
- **pyannote-audio**: Gold standard accuracy (~10% DER), Python/PyTorch only.
- **diart**: Real-time streaming diarization, 500ms latency, Python only.
- **WhisperX**: Whisper + pyannote in one pipeline, batch only, Python.

## TTS Naturalness & Pacing (2026-03-21)

| 2026-03-21 | TTS Naturalness & Pacing | [tts-naturalness-and-pacing.md](tts-naturalness-and-pacing.md) | Sentence boundary artifacts caused by per-sentence generation + direct concatenation; fix via Extra params (frames_after_eos, temperature) and app-level silence injection |

## Transcript Analysis with LLM (2026-03-23)

| 2026-03-23 | Transcript Analysis with LLM | [transcript-analysis-with-llm.md](transcript-analysis-with-llm.md) | Claude subscription cannot be used programmatically (blocked since Jan 2026); best alternative is Ollama + Qwen 2.5 14B locally via OllamaSharp NuGet — zero cost, local-first, ~80-90% of GPT-4 quality for extraction tasks |

## Transcription Engine Overhaul (2026-03-25)

| 2026-03-25 | Transcription Engine Overhaul | [transcription-engine-overhaul.md](transcription-engine-overhaul.md) | For dual-stream calls, replace diarization with VAD-only per stream (mic="You", loopback="Remote"); fixes over-segmentation, ordering, and stability. Add transcription queue with bottom-bar UI. Apply linear drift correction for long recordings. |

## Legal Risks of Commercial Release (2026-03-26)

| 2026-03-26 | Legal Risks of Commercial Release | [legal-risks-commercial-release.md](legal-risks-commercial-release.md) | Recording: user liable, not developer (Sony Betamax); Voice cloning: EU AI Act requires watermarking by Aug 2026, German court ruled AI voice clones violate personality rights; Form UG before release; "as-is" void in Germany; Product Liability Directive covers software from Dec 2026 |

## Monetization & Marketing (2026-03-26)

| 2026-03-26 | Monetization & Marketing | [monetization-and-marketing.md](monetization-and-marketing.md) | Freemium + one-time purchase (proprietary); 2-3 month launch window; 6-phase sequence: relicense → license keys → landing page → content → feature split → launch (Show HN → PH → Reddit); all dependencies allow commercial use; privacy-as-architecture is #1 differentiator |

## Auto-Update & Distribution (2026-03-27)

| 2026-03-27 | Auto-Update & Distribution | [auto-update-and-distribution.md](auto-update-and-distribution.md) | Velopack (free, Rust-based, delta updates) is the right framework; MSIX is wrong for tray apps needing system access; code signing blocked for German individuals — ship unsigned, sign after UG registration |

## MacWhisper Growth Playbook (2026-03-27)

| 2026-03-27 | MacWhisper Growth Playbook | [macwhisper-growth-playbook.md](macwhisper-growth-playbook.md) | Zero paid ads; 250K+ downloads in 17 months via Twitter/X audience, Apple featuring, press (iMore App of Year), Product Hunt, and perfect timing on Whisper hype. Ship fast (2-2-2 method), iterate publicly, each feature = launch event. |

## Pre-Computed Voice Embeddings (2026-03-31)

| 2026-03-31 | Pre-Computed Voice Embeddings | [pre-computed-voice-embeddings.md](pre-computed-voice-embeddings.md) | Pocket TTS Python supports safetensors for instant voice loading, but sherpa-onnx does NOT — requires upstream PR or fork; interim fix: warm up all voices at startup |

## Filler Words & Custom Vocabulary (2026-03-31)

| 2026-03-31 | Filler Words & Custom Vocabulary | [filler-words-and-custom-vocabulary.md](filler-words-and-custom-vocabulary.md) | Regex filter for fillers (dictation only); replacement dictionary for custom vocab (names, terms); hotword boosting blocked on Parakeet TDT in sherpa-onnx; Double Metaphone for phonetic fuzzy matching |

## WAV-to-MP3 Before Transcription (2026-03-31)

| 2026-03-31 | WAV-to-MP3 Before Transcription | [wav-to-mp3-before-transcription.md](wav-to-mp3-before-transcription.md) | Fully feasible at 128 kbps mono — no ASR degradation, diarization safe, 4x storage reduction; use NAudio.Lame NuGet |

## TTS Voice Cloning Models (2026-04-01)

| 2026-04-01 | TTS Voice Cloning Models 2026 | [tts-voice-cloning-models-2026.md](tts-voice-cloning-models-2026.md) | Voxtral (Mistral) blocked by CC BY-NC license; Chatterbox Turbo (MIT, 23 langs incl. German, ONNX) is best upgrade candidate; Pocket TTS German coming; ZipVoice is easiest sherpa-onnx swap |

## Gemma 4 vs Qwen 2.5 for Transcript Analysis (2026-04-09)

| 2026-04-09 | Gemma 4 vs Qwen 2.5 14B | [gemma-4-vs-qwen-2.5-for-transcript-analysis.md](gemma-4-vs-qwen-2.5-for-transcript-analysis.md) | Gemma 4 E4B fits on RTX 3080 10GB but scores ~10pts lower on MMLU (69.4% vs 79.7%) and much worse on instruction following; the competitive Gemma 4 models (26B/31B) need 15-18GB VRAM — stick with Qwen 2.5 14B |

## MacParakeet Feature Comparison (2026-04-20)

| 2026-04-20 | MacParakeet Feature Comparison | [macparakeet-feature-comparison.md](macparakeet-feature-comparison.md) | Main gaps vs MacParakeet: deterministic clean-text pipeline (fillers + custom words + snippet expansion), per-utterance dictation history (unlocks private mode/stats/favorites), export depth (SRT/VTT/JSON/DOCX/PDF), and LLM layer expansion (prompt library + multi-summary + multi-conversation chat); WhisperHeim already has YouTube, dual-capture, diarization, and TTS/voice cloning which MacParakeet lacks |

## UI Framework (2026-03-21)

- **WPF + WPF UI (lepoco/wpfui)**: Mica, Fluent controls, tray icon via WPF-UI.Tray. Best fit for Windows 11 tray app.
- **WinUI 3**: No tray support (open issue since 2020). PowerToys uses it but implements tray in raw C++.
