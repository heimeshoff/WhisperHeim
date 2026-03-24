# Task 074: Transcription Engine Busy Guard

**Status:** Todo
**Priority:** High
**Size:** Small
**Milestone:** --
**Dependencies:** --

## Description

When a transcription is already running (call recording transcription or file transcription), attempting another transcription silently fails — the UI shows "Transcribing" forever but nothing happens.

Add a busy guard:

1. **Engine busy detection** — Track whether the transcription engine is currently in use. The Parakeet model likely cannot handle concurrent requests safely.

2. **UI feedback** — When the engine is busy, transcribe buttons should be grayed out and show a meaningful label like "Engine busy" or "Waiting for engine". No click action while disabled.

3. **Auto-enable on release** — Once the active transcription completes (or fails), all transcribe buttons should become clickable again immediately.

4. **Applies globally** — This guard covers all transcription entry points: file transcription, call recording transcription, and any future transcription triggers. No two transcriptions should run in parallel.

## Why

- The transcription engine (Parakeet TDT) is a single shared resource — concurrent access causes silent failures
- Users see a stuck "Transcribing" state with no way to recover
- Clear feedback prevents confusion and wasted time

## Acceptance Criteria

- [ ] Only one transcription can run at a time across the entire application
- [ ] When engine is busy, all transcribe buttons are grayed out with "Engine busy" label
- [ ] Buttons automatically re-enable when the active transcription completes or fails
- [ ] No silent hangs — if a transcription can't start, the UI clearly communicates why
- [ ] Existing single-transcription flows continue to work as before

## Technical Notes

- The transcription queue from commit 7a4e04c may already serialize call transcriptions — verify if file transcription also goes through this queue or bypasses it
- Consider a simple `IsBusy` flag or semaphore on the transcription service, exposed as an observable property for UI binding
- All transcription entry points must check/acquire the busy state before starting
