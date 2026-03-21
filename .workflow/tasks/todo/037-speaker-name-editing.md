# Task: Speaker Name Editing

**ID:** 037
**Milestone:** M2 - Audio Capture + Call Transcription
**Size:** Medium
**Created:** 2026-03-21
**Dependencies:** 036

## Objective
Allow users to rename speakers in a transcript, both globally (rename all segments from a speaker) and per-segment.

## Details
Add speaker name editing to the transcript viewer. By default, speakers are labeled "You", "Other", or "Speaker N". Users should be able to click on a speaker label and rename it. A global rename updates all segments with that speaker label throughout the transcript. Additionally, allow per-segment override — if a user wants to attribute a single segment to a different speaker, they can change just that one. Persist speaker name mappings in the transcript JSON. Consider adding a speaker name map at the transcript level (original label → custom name) for global renames, while per-segment overrides are stored on individual segments.

## Acceptance Criteria
- [ ] Clicking a speaker label opens an inline edit field
- [ ] Global rename: changing "Other" to "Alice" updates all "Other" segments
- [ ] Per-segment override: can change a single segment's speaker independently
- [ ] Speaker name changes persisted to transcript JSON
- [ ] Speaker colors remain consistent after rename
- [ ] Existing transcripts without custom names load correctly

## Notes
The data model needs a speaker name mapping. Consider a dictionary at the transcript level for global renames, plus an optional override field on `TranscriptSegment`.

## Work Log
<!-- Appended by /work during execution -->
