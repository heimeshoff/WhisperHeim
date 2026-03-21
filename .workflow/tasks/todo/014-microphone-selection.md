# Task: Microphone Selection

**ID:** 014
**Milestone:** M1 - Live Dictation + Core App
**Size:** Small
**Created:** 2026-03-21
**Dependencies:** 003-settings-infrastructure, 006-audio-capture-service

## Objective
Allow the user to select which microphone to use in the settings UI.

## Details
Add a microphone selection dropdown to the Dictation settings page. Enumerate devices via NAudio. Show device names. Default to system default device. Changing the device in settings takes effect on next dictation start (no need for hot-swap). Persist selection in settings.json. Handle the case where a saved device is no longer available (fall back to default, show warning).

## Acceptance Criteria
- [ ] All connected mics listed
- [ ] Selection persists
- [ ] Fallback works for missing devices

## Notes
NAudio device enumeration. No hot-swap needed; takes effect on next dictation start.

## Work Log
<!-- Appended by /work during execution -->
