# Task: Settings Infrastructure

**ID:** 003
**Milestone:** M1 - Live Dictation + Core App
**Size:** Medium
**Created:** 2026-03-21
**Dependencies:** 002-tray-icon-and-window

## Objective
JSON-based settings file with load/save and a settings UI shell with navigation.

## Details
Create a Settings class with JSON serialization (System.Text.Json). Settings file stored in %APPDATA%/WhisperHeim/settings.json. Auto-create with defaults on first run. Settings UI uses WPF UI NavigationView with pages: General, Dictation, Templates, About. For now pages can be mostly empty placeholders. Settings are loaded on startup and saved on change.

## Acceptance Criteria
- [ ] Settings file created on first run with defaults
- [ ] UI shows navigation between pages
- [ ] Changes persist across restarts

## Notes
Use System.Text.Json for serialization. %APPDATA%/WhisperHeim/ as the base directory.

## Work Log
<!-- Appended by /work during execution -->
