# Task: Windows startup auto-launch

**ID:** 024
**Milestone:** M1 - Live Dictation + Core App
**Size:** Small
**Created:** 2026-03-21
**Dependencies:** 002

## Objective
Option to automatically start WhisperHeim on Windows login, minimized to tray.

## Details
Add a toggle in General settings: "Start with Windows". When enabled, create a registry entry in HKCU\Software\Microsoft\Windows\CurrentVersion\Run pointing to the app executable. When disabled, remove the entry. App should start minimized to tray (no window shown) when launched via auto-start. Use a command-line argument (e.g., --minimized) to distinguish auto-start from manual launch. Handle the case where the exe path changes (e.g., after update).

## Acceptance Criteria
- [ ] Toggle in settings enables/disables auto-start
- [ ] Registry entry created/removed correctly
- [ ] App starts minimized to tray on login
- [ ] Manual launch still shows the window normally
- [ ] Works after exe path changes

## Notes
Uses HKCU registry (per-user, no admin required). Alternative: Task Scheduler, but registry is simpler and standard (Chrome, Discord, Steam all use it).

## Work Log
<!-- Appended by /work during execution -->
