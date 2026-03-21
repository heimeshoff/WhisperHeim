# Task: WASAPI Loopback Audio Capture

**ID:** 015
**Milestone:** M2 - Audio Capture + Call Transcription
**Size:** Medium
**Created:** 2026-03-21
**Dependencies:** 006

## Objective
Capture system audio via WASAPI loopback recording.

## Details
Create a LoopbackCaptureService using NAudio's WasapiLoopbackCapture. Capture all system audio (whatever is playing through speakers/headphones). Resample to 16kHz mono to match the ASR pipeline. Store raw audio to a temp WAV file during recording. Expose start/stop/events matching AudioCaptureService interface. Handle the case where no audio device is available.

## Acceptance Criteria
- [ ] System audio captured during a Zoom/YouTube/etc session
- [ ] Resampled correctly to 16kHz mono
- [ ] Saved to WAV file
- [ ] Start/stop works cleanly
- [ ] Handles missing audio device gracefully

## Notes
Uses NAudio's WasapiLoopbackCapture API. Must match the same interface as AudioCaptureService for consistency.

## Work Log
<!-- Appended by /work during execution -->
