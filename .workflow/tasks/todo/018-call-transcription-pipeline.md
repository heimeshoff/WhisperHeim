# Task: Call Transcription Pipeline

**ID:** 018
**Milestone:** M2 - Audio Capture + Call Transcription
**Size:** Large
**Created:** 2026-03-21
**Dependencies:** 016, 017, 008

## Objective
Produce a timestamped, speaker-attributed transcript from a recorded call.

## Details
After call recording stops, run the transcription pipeline: 1) Diarize the combined audio to identify speaker segments, 2) Transcribe each segment with Parakeet TDT, 3) Assemble into a structured transcript with timestamps and speaker labels. Use the dual-stream hint (mic = "You", loopback = "Other") to improve speaker labeling. Show progress during processing. Store transcripts in %APPDATA%/WhisperHeim/transcripts/ with date-based naming. Support cancellation.

## Acceptance Criteria
- [ ] Complete transcript produced with speaker labels and timestamps
- [ ] Progress shown during processing
- [ ] Transcripts stored persistently in %APPDATA%/WhisperHeim/transcripts/
- [ ] Date-based naming for transcript files
- [ ] Cancelable processing

## Notes
Combines diarization (017), dual capture (016), and Parakeet transcription (008) into a unified pipeline.

## Work Log
<!-- Appended by /work during execution -->
