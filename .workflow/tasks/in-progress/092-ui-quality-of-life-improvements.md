# Task 092: UI Quality-of-Life Improvements

**Status:** Done
**Size:** Small
**Created:** 2026-03-27
**Milestone:** --

## Description

Two UI polish fixes for the dictation and conversations screens:

### 1. Template list: single-line descriptions

In the dictation screen template list, multi-line template descriptions currently expand vertically, taking up too much space. The list should show descriptions truncated to a single line with ellipsis. The full content is already visible in the drawer when clicking a template.

**Note:** The XAML already has `TextTrimming="CharacterEllipsis"` and `TextWrapping="NoWrap"` — investigate why multi-line descriptions still expand vertically despite these settings. Likely a container height or row sizing issue.

**File:** `src/WhisperHeim/Views/Pages/DictationPage.xaml` (template list around lines 383-548)

### 2. Imported file: show filename while pending

When importing an audio file via browse, the pending state shows a generic "Call 2024-03-27 14:30" title derived from the session directory timestamp. It should instead show the original filename (without extension) as the title.

The filename is already available — `ImportAudioFile()` stores it as `TranscriptionQueueItem.Title`, but `LoadPendingSessions()` ignores queued item titles and always falls back to parsing the directory name.

**Fix:** In `LoadPendingSessions()`, check if a queued item exists for the pending session and use its `Title` property instead of the generic "Call {date}" format. Recordings started from within the app (not imported) should continue showing "Call {date}".

**Files:**
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` — `LoadPendingSessions()` (lines 513-536), `ImportAudioFile()` (lines 411-455)
- `src/WhisperHeim/Services/Transcription/TranscriptionQueueService.cs` — queue item with Title property

## Acceptance Criteria

- [ ] Template descriptions in the list view never exceed one line, with ellipsis for overflow
- [ ] Full description remains visible in the drawer when clicking a template
- [ ] Imported files show the original filename (without extension) as the title while pending
- [ ] App-initiated recordings still show "Call {date}" while pending
- [ ] After transcription completes, behavior is unchanged (filename is already used)
