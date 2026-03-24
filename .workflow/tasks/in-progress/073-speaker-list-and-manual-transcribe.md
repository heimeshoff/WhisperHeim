# Task 073: Speaker Name List, Count Hint, and Manual Transcription

**Status:** Todo
**Priority:** High
**Size:** Medium
**Milestone:** --
**Dependencies:** --

## Description

Rework the call recording post-processing flow:

1. **No automatic transcription after recording** — After a recording ends, do NOT auto-run the transcription pipeline. Instead, show the recording session with a "Transcribe" button.

2. **Speaker name list** — In the recording review UI, allow the user to define a list of speaker names for the remote participants. This list is editable at any point before or after transcription. The local mic speaker is always "me" (no diarization needed for mic.wav).

3. **Speaker count hint from name list** — When transcribing, pass `numSpeakers=len(speaker_names)` to the loopback diarization instead of `-1` (auto-detect). This constrains clustering to the correct number of remote speakers, fixing over-segmentation (e.g., 18 speakers detected for a single remote participant).

4. **Skip mic diarization** — The mic always has exactly one speaker (the local user). Skip diarization for mic.wav entirely and assign all mic segments to Speaker 0.

5. **Default speaker name setting** — Add a setting in the Settings page where the user can type their own name. This name replaces "You" as the default label for the local mic speaker in all transcripts.

6. **Speaker name selection in transcript** — In the transcript viewer, instead of only typing speaker names by hand, allow selecting from the predefined name list via a dropdown. Manual typing remains as a fallback.

7. **Re-transcribe button** — Add a button to re-run the full pipeline (diarization + transcription) on an existing recording. This lets the user adjust the speaker list and re-process to get better speaker assignment.

## Why

- Diarization over-segments loopback audio when speaker count is unknown (clustering threshold too sensitive for compressed call audio across 120s chunks with independent diarizer instances)
- Auto-transcription wastes compute when the user wants to set up speaker names first
- Typing speaker names by hand in the transcript is tedious when they're already known

## Acceptance Criteria

- [ ] Recording does NOT auto-transcribe — shows "Transcribe" button instead
- [ ] User can define a list of remote speaker names on the recording session
- [ ] Speaker name list is editable before and after transcription
- [ ] Transcribe passes `numSpeakers=N` (from name list length) to loopback diarization
- [ ] Mic.wav diarization is skipped — all mic audio assigned to local user
- [ ] Settings page has a "Default speaker name" field for the local user's name
- [ ] Transcripts use the configured name instead of "You" for the mic speaker (falls back to "You" if empty)
- [ ] Transcript viewer shows a dropdown to select from predefined names (in addition to manual typing)
- [ ] Re-transcribe button re-runs diarization + transcription with current speaker list
- [ ] Works correctly with 0 remote speakers defined (falls back to auto-detect)
- [ ] Existing transcripts remain viewable (backward compatible)

## Technical Notes

- `CallTranscriptionPipeline`: remove auto-trigger after recording stop; add manual trigger path
- `SpeakerDiarizationService.DiarizeDualStreamAsync`: skip mic diarization, just create a single segment spanning full mic audio; pass name list length as `numSpeakers` for loopback
- Settings: add `DefaultSpeakerName` string property, used as mic speaker label (fallback: "You")
- `CallRecordingSession`: add `RemoteSpeakerNames` list property, persist to session metadata
- Transcript viewer: add name dropdown sourced from session's `RemoteSpeakerNames`
- Re-transcribe: reuse existing `CallTranscriptionPipeline.ProcessAsync` with updated session metadata
