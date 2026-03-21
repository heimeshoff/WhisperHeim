# Task: Transcript Naming (Editable Title)

**ID:** 036
**Milestone:** M2 - Audio Capture + Call Transcription
**Size:** Small
**Created:** 2026-03-21
**Dependencies:** 019

## Objective
Allow transcripts to have an editable name/title, displayed at the top of the transcript viewer and in the transcript list.

## Details
Add a `Name` field to the `CallTranscript` data model. Default to a generated name based on date/time (e.g. "Call 2026-03-21 14:30"). Display the name as an editable TextBox at the top of the transcript viewer — clicking it lets the user rename. The transcript list should show the name instead of (or alongside) the date. Persist the name in the transcript JSON. Update the storage service to handle the new field with backward compatibility for existing transcripts (use filename-derived default for old transcripts missing the field).

## Acceptance Criteria
- [ ] `CallTranscript` model has a `Name` property
- [ ] Default name generated from recording start time
- [ ] Name displayed as editable field at top of transcript viewer
- [ ] Name persisted to transcript JSON on edit
- [ ] Transcript list shows the name
- [ ] Existing transcripts without a name get a sensible default

## Notes
Keep backward compatibility — old JSON files without a `name` field should still load fine.

## Work Log
<!-- Appended by /work during execution -->
