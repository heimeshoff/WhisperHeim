# Task: Global Hotkey

**ID:** 004
**Milestone:** M1 - Live Dictation + Core App
**Size:** Small
**Created:** 2026-03-21
**Dependencies:** 002-tray-icon-and-window

## Objective
Register Ctrl+Win as a global hotkey that toggles dictation state.

## Details
Use Win32 RegisterHotKey/UnregisterHotKey via P/Invoke. Register on app startup, unregister on exit. Hotkey press raises an event that other components can subscribe to. Handle the case where hotkey is already registered by another app (show notification). Make the hotkey configurable in settings (store as modifier + key combo). Default: Ctrl+LWin.

## Acceptance Criteria
- [ ] Hotkey fires reliably from any application
- [ ] Event is raised
- [ ] Handles conflicts gracefully
- [ ] Unregisters on exit

## Notes
Win32 RegisterHotKey/UnregisterHotKey via P/Invoke. Default combo: Ctrl+LWin.

## Work Log
<!-- Appended by /work during execution -->
