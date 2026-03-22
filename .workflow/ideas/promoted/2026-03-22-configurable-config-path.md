> **Promoted to task:** 063-configurable-data-path.md on 2026-03-22

# Idea: Configurable Config/Data Path for Cloud Sync

**Captured:** 2026-03-22
**Source:** User input
**Status:** Ready
**Last Refined:** 2026-03-22

## Description
The application's configuration and data should be stored in a configurable path. Only a small bootstrap config file stays in the fixed default location (`%APPDATA%\WhisperHeim\`), pointing to the actual config/data folder. By default, the actual config lives next to the bootstrap file, but the user can change this path in the Settings page to point to a cloud-synced folder (Google Drive, OneDrive, etc.), enabling cross-machine sync.

### Synced folder structure (per-session recordings)
```
[synced path]/
├── settings.json              # App config (shared preferences, templates)
├── recordings/                # Per-session folders
│   ├── 20260322_143000/
│   │   ├── transcript.json
│   │   ├── mic.wav
│   │   └── system.wav
│   └── ...
└── voices/                    # Custom cloned voice samples (.wav)
```

### Bootstrap config (stays in %APPDATA%\WhisperHeim\)
Contains:
- Pointer to the synced data path
- Machine-local settings: WindowSettings, OverlaySettings, selected audio device

### What moves to the synced path
- `settings.json` (general settings, dictation settings, TTS voice preference, templates)
- `recordings/` (transcript JSON + WAV files, per-session folders)
- `voices/` (custom cloned voice samples)

### What stays local
- Bootstrap/pointer config
- `models/` (~800MB, too large to sync, easily re-downloaded)
- `whisperheim.log`
- Window position/size, overlay position (machine-specific)
- Audio device selection (machine-specific)

### Recording lifecycle
- Recordings are first-class data, not temporary files
- Each recording session gets its own folder under `recordings/`
- Deleting a recording deletes the entire session folder (transcript + WAV files)
- No more temp files in `%TEMP%` — recordings go directly to the data path

### Conflict handling
- Last-write-wins for settings.json — no special conflict resolution needed
- Recordings, voices are unique timestamped files — no conflict possible
- User's scenario: notebook + main PC synced via Google Drive, rarely editing settings on both simultaneously

### Path validation
- When the user picks a new path in Settings, validate by writing a temp file
- If not writable, show an error and reject the path
- No ongoing monitoring needed

## Initial Thoughts
- This is a two-tier config pattern: a "pointer" config at the fixed location, and the real config/data at the configurable location
- Settings page needs a folder picker to change the path
- Migration from current location to new path needs consideration
- All path resolution is already centralized in services (SettingsService, TranscriptStorageService, TextToSpeechService, ModelManagerService) — changes should be manageable

## Open Questions
(All resolved — see Refinement Log)

## Refinement Log

### 2026-03-22
Resolved all three open questions through discussion:
1. **What syncs vs. stays local:** Settings, recordings, and voices sync. Models, logs, window/overlay position, and audio device stay local. This means splitting machine-local settings out of settings.json into the bootstrap config.
2. **Conflict handling:** Last-write-wins, no special handling. User syncs between two personal machines via Google Drive and rarely changes settings in parallel.
3. **Path validation:** Lightweight write-test when user picks a new path. Reject if not writable.

Additionally refined the recording architecture:
- Recordings are first-class data, not temp files
- Per-session folder structure (`recordings/YYYYMMDD_HHMMSS/`) containing transcript.json + WAV files
- Deleting a recording deletes the entire session folder
