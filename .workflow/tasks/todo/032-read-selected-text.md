# Task: Read selected text via global hotkey

**ID:** 032
**Milestone:** M4 - Text-to-Speech
**Size:** Medium
**Created:** 2026-03-21
**Dependencies:** 029
**Parent:** 023

## Objective
Register a global hotkey that captures selected text from any application and reads it aloud using the TTS engine.

## Details
- New `SelectedTextService` with cascading capture strategy:
  1. Try UI Automation `TextPattern.GetSelection()` on focused element (clean, no side effects)
  2. Fall back to `SendInput` simulating Ctrl+C, wait 100ms, read clipboard, restore original clipboard
- Use `SendInput` (not `SendKeys`) for reliability
- Clipboard backup/restore: save text + rich text formats, restore after read
- Handle edge cases: empty selection, clipboard locked, Ctrl+C side effects in terminals
- Register hotkey via existing `GlobalHotkeyService` (user-configurable key combo)
- Pass captured text to `ITextToSpeechService` with the user's selected voice
- Requires `UIAutomationClient` and `UIAutomationTypes` references

## Acceptance Criteria
- [ ] Global hotkey captures selected text from browsers (Chrome, Edge, Firefox)
- [ ] Works in Notepad, VS Code, Office applications
- [ ] Original clipboard contents restored after capture
- [ ] Empty selection handled gracefully (no error, optional notification)
- [ ] Captured text passed to TTS and read aloud
- [ ] Hotkey is user-configurable

## Work Log
<!-- Appended by /work during execution -->
