# Research: Filler Word Filtering & Custom Vocabulary

**Date:** 2026-03-31
**Status:** Complete
**Relevance:** Dictation quality (Milestone 1), Transcription quality (Milestone 2)

## Summary

Filler word removal and custom vocabulary correction are two distinct post-processing problems. For filler words, the simplest and most effective approach is a regex-based filter on a curated word list — this is what most competitors do under the hood. For custom vocabulary (proper nouns, names, domain terms), sherpa-onnx's hotword boosting exists but does not currently work with Parakeet TDT due to its stateful decoder architecture. The practical path is a user-editable replacement dictionary, optionally enhanced with phonetic fuzzy matching.

Neither Parakeet TDT nor sherpa-onnx offer built-in filler word suppression or vocabulary customization that works out of the box today. Post-processing is the reliable path for both.

## Key Findings

### 1. Filler Word Filtering

**Parakeet TDT behavior is unknown** — the model card does not document whether it emits filler tokens (uh, um, äh, ähm). Empirical testing is needed. Sherpa-onnx has no built-in filtering.

**Regex-based removal is the recommended approach.** A word-boundary regex matching a curated bilingual list:

```
English: uh, um, uhm, hmm, mm, mhm, mmm, uh-huh, mm-hmm
German:  äh, ähm, öh, öhm, hm, hmm, mhm
```

Pattern: `\b(uh|uhm|um|hmm|mm|mhm|mmm|äh|ähm|öh|öhm)\b` (case-insensitive), followed by double-space cleanup.

**Caveat:** German "um" means "around" — context-free regex will produce false positives. Acceptable for dictation, not for verbatim transcription.

**NLP-based disfluency detection** exists (BERT token classifiers, ~1.3 MiB models) but is English-only and requires ONNX conversion. Overkill for the initial implementation.

**Competitor approaches:**
| Product | Approach |
|---------|----------|
| Whisper | Built-in normalizer removes fillers by default; `suppress_tokens` parameter for control |
| Deepgram | API flag `filler_words=true/false` |
| Azure Speech | Built-in disfluency removal in display text, on by default |
| MacWhisper | Dedicated setting + AI cleanup prompts |
| Windows Voice Typing | No removal — types fillers verbatim |
| Windows 11 Fluid Dictation | On-device SLM removes fillers (requires NPU, English only) |
| Talkativ | Automatic filler removal via AI |

**Best practice:** Remove fillers in dictation mode (on by default), keep them in transcription mode (offer optional "clean up" via LLM).

### 2. Custom Vocabulary / Hotword Boosting

**Sherpa-onnx has hotword boosting** via `HotwordsFile` and `HotwordsScore` on `OfflineRecognizerConfig`. Format:

```
WHISPERHEIM
HEIMESHOFF
PARAKEET :3.5
SHERPA ONNX :2.0
```

**Critical limitation:** Hotwords require `modified_beam_search` decoding on stateless transducers. Parakeet TDT has a stateful decoder — sherpa-onnx's `modified_beam_search` is incompatible. A community fork has demonstrated it working, but it is not merged upstream. This means **hotword boosting does not work with Parakeet TDT today**.

**CTC-WS (Word Spotter)** is a newer, faster approach that matches CTC log-probabilities against a prefix trie. NVIDIA NeMo 2.5+ supports word boosting for TDT via Flashlight decoder, but this is NeMo runtime, not sherpa-onnx.

### 3. Post-Processing Replacement Dictionary

**The most practical immediate approach.** A user-editable file mapping misrecognitions to correct spellings:

```
whisper hyme → WhisperHeim
hymes hoff → Heimeshoff
marco → Marco
```

MacWhisper ships exactly this as "Global Replace Settings." Trivial to implement, deterministic, zero risk.

### 4. Phonetic Fuzzy Matching

**Double Metaphone** can catch misrecognitions not manually catalogued. Build a target vocabulary of proper nouns; after transcription, check each word phonetically against it.

Available .NET libraries:
- **Phonix** (C#, implements Soundex, Metaphone, Double Metaphone, Caverphone)
- **XSoundex** (minimal .NET Soundex)
- Algorithms are simple enough to implement directly (~20-50 lines)

**Sherpa-onnx also has `HomophoneReplacerConfig`** using FST rules (`DictDir`, `Lexicon`, `RuleFsts`). Designed primarily for Chinese but mechanism is language-agnostic. Documentation for English/German is sparse.

### 5. LLM-Based Correction

Tested by Vosk team (March 2025) with 8 LLMs on N-best hypotheses:
- Most 8B models **hallucinated in ~25% of cases**
- Best result: 14.6% WER vs 15.9% baseline (Gemini Flash 2.0 Lite)
- Simple ROVER ensemble voting achieved 14.8% WER with much better stability
- **Proper noun correction is the weakest point** — LLMs overcorrect rare names
- Not recommended for production without significant guardrails

### 6. Fine-Tuning

Impractical for end users: requires 1-14+ hours of transcribed audio, ML expertise, GPU resources, and risks degrading general accuracy. Runtime approaches (replacement dict, hotwords when available) are strictly better for this use case.

## Implications for WhisperHeim

### Dictation Pipeline (`DictationPipeline`)
1. **Phase 1:** Add `FillerWordFilter` with configurable bilingual word list. Apply after transcription, before `SendInput`. Setting: `RemoveFillerWords` (default: true).
2. **Phase 1:** Add `ReplacementDictionary` — user-editable text file of misrecognition→correction mappings. Apply after filler removal.
3. **Phase 2:** Add Double Metaphone fuzzy matching against a user-defined proper noun vocabulary, to catch unlisted misrecognitions.
4. **Watch:** When sherpa-onnx merges Parakeet TDT hotword support, switch to `modified_beam_search` and enable `HotwordsFile`.

### Transcription Pipeline (`CallTranscriptionPipeline`, `FileTranscriptionService`)
1. Keep verbatim text by default.
2. Offer optional "Clean transcript" button using existing Ollama integration with a disfluency-removal + vocabulary-correction prompt.

### UI
- Settings page: filler word toggle, replacement dictionary editor, proper noun vocabulary editor
- Transcription view: optional "Clean up" post-processing button

## Open Questions

- Does Parakeet TDT actually emit filler words in practice? Needs empirical testing with deliberate filler-heavy audio.
- Will sherpa-onnx merge `modified_beam_search` for Parakeet TDT? Track [issue #2541](https://github.com/k2-fsa/sherpa-onnx/issues/2541) and [issue #2753](https://github.com/k2-fsa/sherpa-onnx/issues/2753).
- How well does Double Metaphone work for German names? May need Kölner Phonetik (Cologne phonetics) algorithm instead.
- Is German "um" a real problem in practice or does sentence context prevent false positives?

## Sources

### Filler Words
- [Whisper filler word normalization](https://github.com/openai/whisper/discussions/1174)
- [Whisper suppress_tokens](https://github.com/openai/whisper/discussions/949)
- [CrisperWhisper (keeps fillers with timestamps)](https://github.com/nyrahealth/CrisperWhisper)
- [Deepgram Filler Words docs](https://developers.deepgram.com/docs/filler-words)
- [Azure Disfluency Removal](https://learn.microsoft.com/en-us/answers/questions/2106744/how-to-disable-the-default-disfluency-removal-of-f)
- [Windows 11 Fluid Dictation](https://support.microsoft.com/en-us/topic/fluid-dictation-2810e7d5-1824-44a9-98ce-eb5abcf45691)
- [Disfluency Detection with Small BERT Models](https://ar5iv.labs.arxiv.org/html/2104.10769)
- [awesome-disfluency-detection](https://github.com/pariajm/awesome-disfluency-detection)
- [Daily.co filler word removal](https://www.daily.co/blog/ai-assisted-removal-of-filler-words-from-video-recordings/)

### Custom Vocabulary
- [Sherpa-onnx Hotwords Documentation](https://k2-fsa.github.io/sherpa/onnx/hotwords/index.html)
- [Sherpa-onnx issue #2541 (Parakeet hotwords)](https://github.com/k2-fsa/sherpa-onnx/issues/2541)
- [Sherpa-onnx issue #2753 (Parakeet v2 hotwords)](https://github.com/k2-fsa/sherpa-onnx/issues/2753)
- [NVIDIA NeMo Word Boosting](https://docs.nvidia.com/nemo-framework/user-guide/latest/nemotoolkit/asr/asr_customization/word_boosting.html)
- [Fast Context-Biasing for CTC and Transducer ASR](https://arxiv.org/html/2406.07096v1)
- [Vosk: LLM ASR Error Correction experiments](https://alphacephei.com/nsh/2025/03/15/generative-error-correction.html)
- [MacWhisper Find and Replace](https://macwhisper.helpscoutdocs.com/article/37-find-and-replace-in-transcriptions)
- [Deepgram Keywords](https://developers.deepgram.com/docs/keywords)
- [Azure Phrase Lists](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/improve-accuracy-phrase-list)

### Phonetic Matching
- [Phonix .NET library](https://github.com/eldersantos/phonix)
- [Double Metaphone C# implementation](https://www.codeproject.com/articles/Implement-Phonetic-Sounds-like-Name-Searches-wit-4)
- [HomophoneReplacerConfig.cs (sherpa-onnx)](https://github.com/k2-fsa/sherpa-onnx/blob/master/scripts/dotnet/HomophoneReplacerConfig.cs)
