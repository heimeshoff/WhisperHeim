# Task: Dual Audio Capture for Call Recording

**ID:** 016
**Milestone:** M2 - Audio Capture + Call Transcription
**Size:** Medium
**Created:** 2026-03-21
**Dependencies:** 015

## Objective
Record mic and system audio simultaneously as separate streams for call transcription.

## Details
Create a CallRecordingService that starts both AudioCaptureService (mic) and LoopbackCaptureService (system) simultaneously. Each stream saved to a separate temp WAV file. Synchronize timestamps between streams. Provide a unified start/stop interface. Add a hotkey for call recording (configurable, default: Ctrl+Shift+Win). Show recording duration in the tray tooltip or overlay. Handle one stream failing without losing the other.

## Acceptance Criteria
- [ ] Both streams recorded simultaneously
- [ ] Timestamps aligned between mic and system audio streams
- [ ] Separate WAV files produced for each stream
- [ ] Hotkey works (default: Ctrl+Shift+Win, configurable)
- [ ] Recording duration visible in tray tooltip or overlay
- [ ] One stream failing does not lose the other

## Notes
Depends on LoopbackCaptureService (015) and the existing AudioCaptureService for mic input.

## Work Log
<!-- Appended by /work during execution -->
