# Task: Audio Capture Service

**ID:** 006
**Milestone:** M1 - Live Dictation + Core App
**Size:** Medium
**Created:** 2026-03-21
**Dependencies:** 001-project-scaffolding

## Objective
Capture microphone audio as 16kHz mono PCM using NAudio with a ring buffer.

## Details
Create an AudioCaptureService that wraps NAudio's WaveInEvent. Enumerate available input devices. Capture at 16kHz, 16-bit mono. Convert to float32 normalized samples. Use a thread-safe ring buffer (e.g., ConcurrentQueue or custom circular buffer) to decouple capture from processing. Expose events: AudioDataAvailable(float[] samples), CaptureStarted, CaptureStopped. Handle device disconnection gracefully.

## Acceptance Criteria
- [ ] Audio captured from selected mic
- [ ] 16kHz mono verified
- [ ] Ring buffer works under load
- [ ] Device enumeration works
- [ ] Start/stop is clean

## Notes
NAudio WaveInEvent, 16kHz 16-bit mono, float32 normalized output.

## Work Log
<!-- Appended by /work during execution -->
