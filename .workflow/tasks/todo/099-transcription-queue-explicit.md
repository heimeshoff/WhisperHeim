# Task 099: Explicit Transcription Queuing

**Size:** Small
**Status:** Backlog
**Created:** 2026-04-07
**Milestone:** M2
**Dependencies:** 075, 098

## Description

Change pending transcription items to require explicit user action to queue for transcription, and ensure multiple items queue properly instead of only transcribing the last-clicked one.

## Problem

- Clicking multiple pending items currently only transcribes the last one clicked (UI-level "last-wins" effect)
- Items should not auto-enqueue on click -- the user needs the drawer (task 098) to review, name, and add speakers before deciding to transcribe
- The existing `TranscriptionQueueService` already supports FIFO queuing; the issue is in the UI layer

## Requirements

### 1. Remove auto-enqueue on click

- `PendingRow_Click()` should ONLY open the pending drawer (task 098)
- Remove the current behavior that silently re-enqueues items on click

### 2. Queue via explicit button only

- Transcription is enqueued only when the user clicks "Queue Transcription" in the pending drawer
- The queue service's existing FIFO processing handles ordering

### 3. Visual queue state in list

- Pending items that have been queued show a distinct visual state (e.g., "Queued" badge or different icon) vs items that are just pending/unqueued
- Leverage the existing bottom bar queue visibility -- items appear there once queued
- Unqueued pending items remain in the pending section of the list

## Key Files

- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` -- `PendingRow_Click()` (~line 782), `LoadPendingSessions()` (~line 541)
- `src/WhisperHeim/Services/Transcription/TranscriptionQueueService.cs` -- `Enqueue()` (~line 294), `ProcessNext()` (~line 434)
- `src/WhisperHeim/Views/Controls/TranscriptionBottomBar.xaml.cs` -- existing queue visibility

## Acceptance Criteria

- [ ] Clicking a pending item opens the drawer without starting transcription
- [ ] "Queue Transcription" button in drawer is the only way to enqueue a pending item
- [ ] Multiple items can be queued and process in FIFO order
- [ ] Queued items show distinct visual state vs unqueued pending items in the list
- [ ] Queued items appear in the bottom bar as before
- [ ] No "last-wins" behavior -- all queued items eventually get transcribed

## Work Log
