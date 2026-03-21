# Task: Parakeet ASR Integration

**ID:** 008
**Milestone:** M1 - Live Dictation + Core App
**Size:** Medium
**Created:** 2026-03-21
**Dependencies:** 005-model-manager

## Objective
Transcribe audio segments using Parakeet TDT 0.6B via sherpa-onnx.

## Details
Create a TranscriptionService that loads Parakeet TDT via sherpa-onnx's OfflineRecognizer API. Accept float32 audio samples, return transcribed text. Support both German and English (auto-detect or configurable). Measure and log transcription latency and real-time factor. Handle model loading errors gracefully. Run transcription on a background thread to avoid blocking the audio pipeline.

## Acceptance Criteria
- [ ] Transcribes English and German speech accurately
- [ ] Latency under 500ms for 3-5 second segments on GPU
- [ ] Graceful error handling
- [ ] Non-blocking

## Notes
sherpa-onnx OfflineRecognizer API. Parakeet TDT 0.6B. Background thread for transcription.

## Work Log
<!-- Appended by /work during execution -->
