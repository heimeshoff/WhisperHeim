# Task 098: Pending Transcription Drawer with Playback

**Size:** Medium
**Status:** Backlog
**Created:** 2026-04-07
**Milestone:** M2
**Dependencies:** 095, 097

## Description

Clicking a pending (not-yet-transcribed) recording opens an empty drawer. It should open the same drawer UI as active recordings, with full editing capabilities and audio playback with scrubbing.

## Problem

- `OpenPendingTranscribingDrawer()` opens the drawer but the content is empty/non-functional
- No way to edit the name or add speakers before transcription
- No way to listen to the recording to remember what it was about
- Clicking a pending item currently auto-enqueues it for transcription (wrong behavior -- see task 099)

## Requirements

### 1. Pending drawer shows full editing UI

When clicking a pending transcription item, the drawer should show:
- Editable transcript name field
- Speaker name list with add/remove (same UX as recording drawer)
- All fields pre-populated if the user already entered data during recording

### 2. Audio playback with scrubbing

- Play/pause button for the recorded audio
- Seek bar / scrubber so the user can jump to any point in the recording
- Current position and total duration display
- Uses existing `TranscriptAudioPlayer` service

### 3. Explicit "Queue Transcription" button

- A prominent button at the bottom of the pending drawer: "Transcribe" or "Queue Transcription"
- Clicking it enqueues the session for transcription (with the edited name and speakers)
- Clicking a pending item in the list should ONLY open the drawer, NOT auto-enqueue

### 4. Drawer state management

- Pending drawer is visually distinct from active recording (no recording timer, no red indicator)
- After clicking "Queue Transcription", the drawer can close or transition to show queued status

## Key Files

- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` -- `OpenPendingTranscribingDrawer()` (~line 353), `PendingRow_Click()` (~line 782)
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` -- drawer XAML layout
- `src/WhisperHeim/Services/Audio/TranscriptAudioPlayer.cs` -- existing playback service (supports seek via `CurrentPosition` property)

## Acceptance Criteria

- [ ] Clicking a pending item opens the drawer with name field, speaker list, and playback controls
- [ ] Name and speakers are pre-populated from recording session data
- [ ] Audio playback works with play/pause, seek bar, and position display
- [ ] "Queue Transcription" button enqueues the session with current name/speakers
- [ ] Clicking a pending item does NOT auto-start transcription
- [ ] Drawer works correctly for multiple pending items (open one, close, open another)

## Work Log
