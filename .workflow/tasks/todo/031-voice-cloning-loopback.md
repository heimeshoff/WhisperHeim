# Task: Voice cloning from system audio loopback

**ID:** 031
**Milestone:** M4 - Text-to-Speech
**Size:** Medium
**Created:** 2026-03-21
**Dependencies:** 029
**Parent:** 023

## Objective
Allow users to capture system audio (YouTube, podcasts, etc.) as reference for voice cloning.

## Details
- WASAPI loopback capture at native quality (48kHz, not downsampled to 16kHz)
- Reuse `LoopbackCaptureService` pattern but with high-quality output path
- UI: select output device, start/stop capture, duration display
- After capture, extract voice state and save as `.safetensors`
- Same voice management as mic cloning (name, preview, delete)
- Consider adding a "trim" feature to select the best 5-30 second segment

## Acceptance Criteria
- [ ] Can capture system audio at native quality (48kHz)
- [ ] Capture UI with device selection, start/stop, duration
- [ ] Voice state extracted from captured audio
- [ ] Custom voice saved and available in voice selector
- [ ] Works with common audio sources (browser, media players)

## Work Log
<!-- Appended by /work during execution -->
