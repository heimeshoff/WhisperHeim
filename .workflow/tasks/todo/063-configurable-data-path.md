# Task 063: Configurable Config/Data Path for Cloud Sync

**Status:** Todo
**Priority:** Medium
**Size:** Medium
**Created:** 2026-03-22

## Description
Enable the app's user data to be stored at a configurable path for cloud sync (Google Drive, OneDrive, etc.). A small bootstrap config stays in `%APPDATA%\WhisperHeim\`, pointing to the actual data folder. By default, the data folder is co-located with the bootstrap config.

### Synced folder structure
```
[synced path]/
├── settings.json              # Shared preferences, templates
├── recordings/                # Per-session folders
│   ├── 20260322_143000/
│   │   ├── transcript.json
│   │   ├── mic.wav
│   │   └── system.wav
│   └── ...
└── voices/                    # Custom cloned voice samples
```

### Bootstrap config (stays in %APPDATA%\WhisperHeim\)
- Pointer to the synced data path
- Machine-local settings: WindowSettings, OverlaySettings, audio device selection

### What syncs
- `settings.json` (general settings, dictation settings, TTS voice preference, templates)
- `recordings/` (transcript JSON + WAV files, per-session folders)
- `voices/` (custom cloned voice samples)

### What stays local
- Bootstrap/pointer config
- `models/` (~800MB, re-downloadable)
- `whisperheim.log`
- Window position/size, overlay position, audio device (machine-specific)

## Acceptance Criteria
- [ ] Bootstrap config in `%APPDATA%\WhisperHeim\` with a `dataPath` pointer
- [ ] Default data path co-located with bootstrap config (no change for existing users)
- [ ] Settings page has a folder picker to change the data path
- [ ] Path validation: write a temp file to verify the path is writable before accepting
- [ ] Split machine-local settings (WindowSettings, OverlaySettings, audio device) into bootstrap config
- [ ] Recordings use per-session folders (`recordings/YYYYMMDD_HHMMSS/`) with transcript.json + WAV files
- [ ] Deleting a recording deletes the entire session folder (transcript + WAVs)
- [ ] No more temp recordings in `%TEMP%` — recordings go directly to the data path
- [ ] Migration: move existing data from old flat structure to new structure on first run
- [ ] All path-resolving services updated (SettingsService, TranscriptStorageService, TextToSpeechService, ModelManagerService)

## Technical Notes
- Conflict handling: last-write-wins for settings.json, no special logic needed
- User scenario: notebook + desktop synced via Google Drive
- Path resolution is already centralized in services — changes should be manageable
- Recordings, voices are unique timestamped files — no sync conflicts possible

## Refined from
ideas/2026-03-22-configurable-config-path.md
