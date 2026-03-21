# Task: Input Simulation

**ID:** 010
**Milestone:** M1 - Live Dictation + Core App
**Size:** Medium
**Created:** 2026-03-21
**Dependencies:** 001-project-scaffolding

## Objective
Type text into any focused Windows application using SendInput.

## Details
Create an InputSimulator service using Win32 SendInput P/Invoke. Convert string to sequence of keyboard events (KEYDOWN/KEYUP for each character). Handle Unicode characters via KEYEVENTF_UNICODE flag. Support backspace (for correcting partial results). Consider typing speed -- too fast can overwhelm some applications. Add a small configurable delay between keystrokes if needed. Handle special characters and newlines.

## Acceptance Criteria
- [ ] Text appears correctly in Notepad, terminal (Windows Terminal), browser text fields, Word, and VS Code
- [ ] Unicode characters work
- [ ] Backspace correction works

## Notes
Win32 SendInput with KEYEVENTF_UNICODE for broad character support. Configurable keystroke delay.

## Work Log
<!-- Appended by /work during execution -->
