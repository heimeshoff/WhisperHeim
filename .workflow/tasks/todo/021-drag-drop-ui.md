# Task: Drag-and-Drop Transcription UI

**ID:** 021
**Milestone:** M3 - Voice Message Transcription
**Size:** Medium
**Created:** 2026-03-21
**Dependencies:** 020

## Objective
Drag-and-drop audio files onto the app window for transcription with batch support.

## Details
Add a "Transcribe Files" page/panel to the main window. Support drag-and-drop (files dropped onto the window). Also support a file picker button. Show transcription progress per file. Display results inline -- each file with its transcript, copy button, duration. Support batch: drop multiple files, transcribe sequentially. Also support drag-and-drop onto the tray icon (if WPF-UI.Tray supports it, otherwise window only). Results are ephemeral (not persisted like call transcripts) unless user explicitly saves.

## Acceptance Criteria
- [ ] Drag-drop works from file explorer and WhatsApp Web
- [ ] Batch transcription works for multiple files
- [ ] Results shown inline with copy button per file
- [ ] File picker fallback button works
- [ ] Transcription progress displayed per file
- [ ] Results are ephemeral unless explicitly saved

## Notes
Results are not persisted by default (unlike call transcripts from 018/019). User must explicitly save if desired.

## Work Log
<!-- Appended by /work during execution -->
