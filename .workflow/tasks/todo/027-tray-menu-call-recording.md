# Task: Tray context menu for start/stop call recording

**ID:** 027
**Milestone:** M2 - Audio Capture + Call Transcription
**Size:** Medium
**Created:** 2026-03-21
**Dependencies:** 026-wire-call-recording-services

## Objective
User can start/stop call recording from the tray context menu or via Ctrl+Win+R hotkey, with visual feedback during recording.

## Details

### Context menu
- Add a "Start Call Recording" menu item to the tray `ContextMenu` in `MainWindow.xaml`, between "Settings" and the separator
- Use a microphone/record icon (e.g., `Record24` or `Mic24` SymbolIcon)
- While recording, change the menu item text to "Stop Call Recording (MM:SS)" with live duration
- Wire the `Click` handler to toggle recording via `CallRecordingService.ToggleRecording()`

### Hotkey
- Register `CallRecordingHotkeyService` in `MainWindow.SetupHotkeysAndOrchestration()`
- Use **Ctrl+Win+R** (not the default Ctrl+Shift+Win+R) — create a custom `HotkeyRegistration` with `ModifierKeys.Control | ModifierKeys.Win` and VirtualKey `0x52`
- Hotkey toggles recording same as menu click

### Recording state feedback
- Subscribe to `CallRecordingService.RecordingStarted`, `RecordingStopped`, `DurationUpdated`, `StreamFailed`
- While recording: update tray icon (use a distinct color, e.g., orange or pulsing), update tooltip to "WhisperHeim - Recording call (MM:SS)"
- On `DurationUpdated`: refresh the context menu item text with current duration
- On `StreamFailed`: show a brief notification or log warning (don't stop the other stream)
- On stop: restore idle tray icon and tooltip, reset menu item text

## Acceptance Criteria
- [ ] Tray context menu shows "Start Call Recording" item with icon
- [ ] Clicking the menu item starts dual-stream recording (mic + loopback)
- [ ] During recording, menu item shows "Stop Call Recording (MM:SS)" with live duration
- [ ] Clicking again stops recording
- [ ] Ctrl+Win+R hotkey toggles recording
- [ ] Tray icon/tooltip changes during recording
- [ ] Tray icon/tooltip resets when recording stops

## Notes
- Ensure dictation hotkey (Ctrl+Win) and call recording hotkey (Ctrl+Win+R) don't conflict
- Use `Dispatcher.Invoke` for UI updates from service event callbacks
- The `RecordingStopped` event will be consumed by task 028 to trigger transcription

## Work Log
<!-- Appended by /work during execution -->
