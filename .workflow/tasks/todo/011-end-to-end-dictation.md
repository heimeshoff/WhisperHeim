# Task: End-to-End Dictation

**ID:** 011
**Milestone:** M1 - Live Dictation + Core App
**Size:** Medium
**Created:** 2026-03-21
**Dependencies:** 004-global-hotkey, 009-streaming-dictation-pipeline, 010-input-simulation

## Objective
Pressing the hotkey starts dictation; spoken text appears at the cursor; pressing again stops.

## Details
Wire the global hotkey to the dictation pipeline. First press: start audio capture + pipeline, update tray icon to "recording" state. Pipeline partial/final results feed into InputSimulator. Second press: stop pipeline, finalize any pending text, restore tray icon. Handle edge cases: hotkey pressed while pipeline is starting, rapid double-press, error during transcription. Tray icon should change color/icon to indicate active dictation state.

## Acceptance Criteria
- [ ] Full flow works: hotkey -> speak -> text appears -> hotkey -> stops
- [ ] Works across multiple applications
- [ ] Tray icon reflects state
- [ ] No crashes on rapid toggle

## Notes
Toggle behavior: first press starts, second press stops. Tray icon state change for visual feedback.

## Work Log
<!-- Appended by /work during execution -->
