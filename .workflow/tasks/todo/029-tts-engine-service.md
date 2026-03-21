# Task: Pocket TTS engine service + model download

**ID:** 029
**Milestone:** M4 - Text-to-Speech
**Size:** Medium
**Created:** 2026-03-21
**Dependencies:** None
**Parent:** 023

## Objective
Create the core TTS service using Kyutai Pocket TTS via sherpa-onnx, with model auto-download and built-in voice playback.

## Details
- Add `ITextToSpeechService` interface with `GenerateAudioAsync(text, voiceState)` and streaming variant
- Implement via sherpa-onnx Pocket TTS C# bindings (check if sherpa-onnx NuGet already supports Pocket TTS, otherwise use ONNX model directly)
- Register Pocket TTS ONNX model in `ModelManagerService` for auto-download from HuggingFace
- Support 8 built-in voices (alba, marius, javert, jean, fantine, cosette, eponine, azelma)
- Audio playback via NAudio `WaveOutEvent` at 24kHz (Pocket TTS native sample rate)
- Expose `GetAvailableVoices()` listing built-in + custom voices

## Acceptance Criteria
- [ ] Pocket TTS ONNX model downloads automatically on first use
- [ ] Can generate speech from text string with any built-in voice
- [ ] Can play generated audio through speakers via NAudio
- [ ] Streaming generation works (first audio chunk within ~200ms)
- [ ] Service is injectable and follows existing patterns

## Work Log
<!-- Appended by /work during execution -->
