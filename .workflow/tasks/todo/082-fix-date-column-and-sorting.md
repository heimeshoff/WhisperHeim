# Task 082: Fix Date Column & Add Column Sorting

**Status:** Todo
**Size:** Medium
**Created:** 2026-03-25

## Problem

The "Date" column in the recordings list shows "transcript" for all new-format recordings instead of the actual recording date. This is because new-format transcripts are stored as `transcript.json` inside `YYYYMMDD_HHmmss/` session directories, and the date parser only handles the legacy `transcript_YYYYMMDD_HHmmss.json` filename format.

Additionally, there is no way to sort recordings within groups by clicking column headers.

## Acceptance Criteria

### Date Column Fix
- [ ] New-format recordings show the actual recording date (from `CallTranscript.RecordingStartedUtc` or the session directory name)
- [ ] Legacy-format recordings continue to work as before
- [ ] Date display format remains `MMM dd, yyyy HH:mm`
- [ ] GroupKey and GroupDisplayName are correctly derived from the actual date

### Column Sorting
- [ ] Clicking a column header (Title, Duration, Date) sorts items within each group by that column
- [ ] Clicking the same column header again toggles between ascending and descending order
- [ ] Visual indicator on the active sort column showing sort direction (e.g., arrow up/down)
- [ ] Default sort order: Date descending (newest first within group)

## Implementation Notes

**Key files:**
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` — column headers, list template
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` — `TranscriptListItem` constructor (date parsing at ~line 1513), `ApplyFilter()` grouping/sorting logic
- `src/WhisperHeim/Services/CallTranscription/CallTranscript.cs` — `RecordingStartedUtc` field
- `src/WhisperHeim/Services/CallTranscription/TranscriptStorageService.cs` — file discovery

**Date fix approach:**
- For new-format files, parse the session directory name (`YYYYMMDD_HHmmss`) to extract the date
- Alternatively, deserialize the transcript JSON and use `RecordingStartedUtc`
- Update `TranscriptListItem` constructor to handle both formats

**Sorting approach:**
- Make column headers clickable (Button or clickable TextBlock)
- Track current sort column and direction in page state
- Apply sort in `ApplyFilter()` after grouping, before rendering
