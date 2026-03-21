# Task: Silero VAD Integration

**ID:** 007
**Milestone:** M1 - Live Dictation + Core App
**Size:** Medium
**Created:** 2026-03-21
**Dependencies:** 006-audio-capture-service

## Objective
Integrate Silero VAD to detect speech boundaries in the audio stream.

## Details
Load Silero VAD ONNX model via ONNX Runtime. Process audio chunks from the ring buffer (512 or 1536 samples at 16kHz). Expose events: SpeechStarted, SpeechEnded(float[] speechAudio). Accumulate speech audio between SpeechStarted and SpeechEnded. Configure thresholds: speech probability threshold (~0.5), min speech duration (~250ms), min silence duration for end-of-speech (~500ms). These should be in settings.

## Acceptance Criteria
- [ ] VAD correctly detects speech start/end
- [ ] No false triggers on silence
- [ ] Speech segments are accumulated and emitted
- [ ] Thresholds are configurable

## Notes
Silero VAD ONNX model, chunk sizes 512 or 1536 samples at 16kHz. Thresholds stored in settings.

## Work Log
<!-- Appended by /work during execution -->
