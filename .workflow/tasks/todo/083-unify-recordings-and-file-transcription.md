# Task 083: Unify Recordings & File Transcription

**Status:** Todo
**Size:** Large
**Milestone:** M3 (Voice Message Transcription)
**Dependencies:** 075 (queue), 076 (auto-transcribe), 071 (drawer)

## Description

Merge the separate "Transcriptions" page into the Recordings page so that imported audio files become first-class recording entries. The Transcriptions nav item and TranscribeFilesPage go away entirely.

## Acceptance Criteria

### UI Changes — Recordings Page
- [ ] Two buttons below the hotkey subline ("Press Ctrl+Win+R to start/stop"):
  - **Start Recording / Stop Recording** — toggles based on current recording state
  - **Browse** — opens a file picker for importing audio files (.ogg, .mp3, .m4a, .wav)
- [ ] Remove the "Transcriptions" navigation item from the sidebar

### File Import Flow
- [ ] On file selection, **move** the file into a new session directory under `recordings/` (format: `YYYYMMDD_HHmmss/`)
  - If move fails (cross-drive, network share), fall back to **copy** (do not delete the original)
- [ ] Create the session directory and place the audio file inside it (as the single audio file, no mic/system split)
- [ ] Create a `transcript.json` stub or mark it as pending so it appears in the recordings list immediately
- [ ] Enqueue the file for transcription via the existing `TranscriptionQueueService`

### Transcription Behavior
- [ ] Default: **no diarization** for imported files (single-speaker, flat transcription)
- [ ] Use the existing `FileTranscriptionService` pipeline for the initial transcription
- [ ] Produce a `CallTranscript`-compatible `transcript.json` with:
  - Title derived from the original filename
  - Duration from audio file metadata
  - Date from file import time
  - Single speaker segments (no speaker labels needed for single-speaker mode)
  - `AudioFilePath` pointing to the moved/copied audio file

### In-List Appearance
- [ ] While transcribing: shown like a recording in progress — title in the row, duration and date visible, "Transcribing..." state indicator
- [ ] Once complete: indistinguishable from a call recording entry — title, duration, date, and transcript shown in the drawer on click

### Drawer — Re-transcribe with Diarization
- [ ] The existing re-transcribe button in the drawer should work for imported files
- [ ] If the user has defined more than one speaker (via speaker name editing), re-transcription triggers the **call transcription pipeline with diarization**
- [ ] Single-speaker re-transcription stays on the flat pipeline

### Cleanup
- [ ] Remove `TranscribeFilesPage.xaml` and `TranscribeFilesPage.xaml.cs`
- [ ] Remove `TranscribeFilesViewModel` and `TranscriptionItemViewModel`
- [ ] Remove the "Transcriptions" case from MainWindow navigation
- [ ] Remove or repurpose `FileTranscriptionService` (keep if still used by the queue for single-file transcription, remove if fully replaced)
- [ ] Clean up any dead references in DI registration / MainWindow

## Technical Notes

- The `CallTranscriptionPipeline` expects dual streams (mic + system). For imported single-file diarization, the file should be fed as the system/loopback stream with no mic stream, so diarization treats all speakers as "remote."
- The `TranscriptionQueueService` already supports both call and file items — the key change is that file items now produce `CallTranscript` JSON instead of ephemeral text results.
- Move semantics: use `File.Move` first, catch `IOException`, fall back to `File.Copy`.

## Open Questions

- None — all clarified during capture.
