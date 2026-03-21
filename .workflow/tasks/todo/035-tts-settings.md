# Task: TTS settings + hotkey configuration

**ID:** 035
**Milestone:** M4 - Text-to-Speech
**Size:** Small
**Created:** 2026-03-21
**Dependencies:** 029, 032
**Parent:** 023

## Objective
Add TTS-specific settings to the settings page and persist them.

## Details
- Extend `AppSettings` model with TTS section:
  - Default voice (built-in or custom voice name)
  - Read-aloud hotkey (key combination)
  - Playback device (audio output device selection)
- Add TTS section to existing Settings page
- Hotkey configuration: key combo picker similar to existing dictation hotkey
- Playback device selector: enumerate output devices via NAudio `WaveOutEvent`
- Persist via existing `SettingsService`

## Acceptance Criteria
- [ ] Default voice setting persists across sessions
- [ ] Read-aloud hotkey is configurable
- [ ] Playback device is selectable
- [ ] Settings appear in existing Settings page
- [ ] Changes take effect immediately (no restart)

## Work Log
<!-- Appended by /work during execution -->
