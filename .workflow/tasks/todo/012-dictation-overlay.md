# Task: Dictation Overlay

**ID:** 012
**Milestone:** M1 - Live Dictation + Core App
**Size:** Medium
**Created:** 2026-03-21
**Dependencies:** 011-end-to-end-dictation

## Objective
Show a small, animated on-screen indicator during active dictation that is visible but non-intrusive.

## Details
Create a small always-on-top, click-through WPF window (borderless, transparent background). Position near the bottom-center of the screen (or configurable). Show a pulsing microphone icon or animated waveform visualization during recording. Use subtle animation -- gentle pulse or breathing effect when listening, faster animation when speech is detected. The overlay should be tiny (~40-60px). Fade in on dictation start, fade out on stop. Make it configurable: enable/disable, position, size, opacity. Should not steal focus or interfere with typing.

## Acceptance Criteria
- [ ] Overlay appears on dictation start
- [ ] Animates during speech
- [ ] Fades on stop
- [ ] Does not steal focus
- [ ] Does not block clicks
- [ ] Is toggleable in settings

## Notes
Click-through via WS_EX_TRANSPARENT extended window style. ~40-60px size. Configurable position and opacity.

## Work Log
<!-- Appended by /work during execution -->
