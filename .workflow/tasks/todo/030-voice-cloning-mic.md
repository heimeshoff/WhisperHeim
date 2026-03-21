# Task: Voice cloning from microphone recording

**ID:** 030
**Milestone:** M4 - Text-to-Speech
**Size:** Medium
**Created:** 2026-03-21
**Dependencies:** 029
**Parent:** 023

## Objective
Allow users to record their voice (or any voice via mic) at high quality and create a custom Pocket TTS voice clone.

## Details
- High-quality mic capture at 44.1/48kHz (separate from Whisper's 16kHz path) — may need a configurable `AudioCaptureService` or a dedicated `HighQualityRecorder`
- Recording UI: start/stop, level meter, duration display (minimum 5 seconds)
- After recording, extract voice state via Pocket TTS voice conditioning
- Save voice state as `.safetensors` in `%APPDATA%/WhisperHeim/voices/`
- User names the voice, can preview it with a test phrase
- Warn user about background noise (Kyutai recommends clean samples)

## Acceptance Criteria
- [ ] Can record mic audio at 44.1kHz+ quality
- [ ] Recording UI shows level, duration, and minimum-length indicator
- [ ] Voice state extracted and saved as .safetensors
- [ ] Custom voice appears in voice selector
- [ ] Can preview custom voice with test phrase
- [ ] Warning displayed about audio quality requirements

## Work Log
<!-- Appended by /work during execution -->
