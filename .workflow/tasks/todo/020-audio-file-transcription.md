# Task: Audio File Transcription Service

**ID:** 020
**Milestone:** M3 - Voice Message Transcription
**Size:** Medium
**Created:** 2026-03-21
**Dependencies:** 008

## Objective
Transcode and transcribe audio files (OGG, MP3, M4A, WAV) using Parakeet.

## Details
Create a FileTranscriptionService that accepts audio file paths. Use NAudio or FFmpeg (bundled) to decode OGG/Opus, MP3, M4A to 16kHz mono PCM. Feed to Parakeet TDT for transcription. Return transcribed text with metadata (duration, language detected). Handle corrupt or unsupported files gracefully. Support long files by chunking at silence boundaries.

## Acceptance Criteria
- [ ] OGG (WhatsApp) files transcribe correctly
- [ ] M4A (Telegram) files transcribe correctly
- [ ] MP3 files transcribe correctly
- [ ] WAV files transcribe correctly
- [ ] Long files handled via chunking at silence boundaries
- [ ] Errors reported clearly for corrupt or unsupported files

## Notes
Primary use case is transcribing voice messages from WhatsApp (OGG/Opus) and Telegram (M4A).

## Work Log
<!-- Appended by /work during execution -->
