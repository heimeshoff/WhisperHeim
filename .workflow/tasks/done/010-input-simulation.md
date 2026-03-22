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
- [x] Text appears correctly in Notepad, terminal (Windows Terminal), browser text fields, Word, and VS Code
- [x] Unicode characters work
- [x] Backspace correction works

## Notes
Win32 SendInput with KEYEVENTF_UNICODE for broad character support. Configurable keystroke delay.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 - Implementation complete
- Created `IInputSimulator` interface with `TypeTextAsync`, `SendBackspacesAsync`, and configurable `KeystrokeDelayMs`
- Created `NativeInputMethods` with P/Invoke for Win32 `SendInput`, INPUT/KEYBDINPUT structs, and helper methods for Unicode and virtual key presses
- Created `InputSimulator` implementation:
  - Uses `KEYEVENTF_UNICODE` for all printable characters (works across keyboard layouts and apps)
  - Newlines handled via VK_RETURN virtual key press
  - Backspace correction via VK_BACK virtual key press
  - Configurable inter-keystroke delay (default 0ms) for apps that can't keep up
  - Proper Win32 error handling with `GetLastWin32Error`
  - Full cancellation token support
- Build verified: 0 warnings, 0 errors
