# Task: Active Recording Card + Auto-Transcribe on Stop

**ID:** 076
**Milestone:** M2 - Audio Capture + Call Transcription
**Size:** Medium
**Created:** 2026-03-25
**Dependencies:** 075, 073

## Objective
Show the active recording session as a card in the Transcripts page, allow editing metadata (title, speaker count, speaker names) during recording, and auto-enqueue for transcription when recording stops.

## Details

### Active Recording Card
- When a recording is in progress, show it as a distinct card at the top of the Transcripts list (above pending and completed transcripts)
- Visual distinction from pending/completed items (e.g., pulsing recording indicator, red accent, live duration counter)
- Clicking the active recording card opens a detail view (in the drawer) where the user can:
  - Set/edit the session title
  - Set the number of remote speakers
  - Enter names for each remote speaker
- Clicking the card does NOT start transcription (unlike pending items)
- The tray icon click during recording should open the main window normally, allowing navigation to the Transcripts page to see and edit the active recording

### Auto-Transcribe on Recording Stop
- When the user stops a recording (via tray icon or hotkey), the recording session is automatically enqueued into the transcription queue (task 075)
- The queue item carries the metadata (title, speaker names, speaker count) that was entered during recording
- If no speaker names were provided, the queue item still processes with auto-detect fallback
- No manual "Transcribe" click needed -- stopping the recording is the trigger

### Pending Recordings Cleanup
- Existing "pending" recordings (recorded but not yet transcribed) should also be enqueueable via the queue
- The manual transcription trigger on pending items should enqueue into the queue rather than launching a modal dialog

## Acceptance Criteria
- [ ] Active recording session visible as a card in Transcripts page
- [ ] Card shows live duration, recording indicator, and session metadata
- [ ] Clicking active card opens drawer with editable title, speaker count, and speaker names
- [ ] Clicking active card does NOT trigger transcription
- [ ] Stopping a recording auto-enqueues it for transcription with pre-filled metadata
- [ ] Pending recordings can still be manually enqueued
- [ ] Tray icon click during recording opens the main window normally

## Notes
- Task 073 already added speaker name list and manual transcription trigger -- build on that work
- The speaker name count implicitly sets `NumClusters` for diarization (task 077)
- Research file: `.workflow/research/transcription-engine-overhaul.md`

## Work Log
<!-- Appended by /work during execution -->
