# Task: Audio export (MP3/OGG)

**ID:** 034
**Milestone:** M4 - Text-to-Speech
**Size:** Small
**Created:** 2026-03-21
**Dependencies:** 029, 033
**Parent:** 023

## Objective
Export generated TTS audio to MP3 or OGG files.

## Details
- "Save as..." button on TTS UI page
- Save dialog with format selection: MP3, OGG, WAV
- MP3 encoding via NAudio `MediaFoundationEncoder` (uses Windows built-in codec)
- OGG/Opus encoding via Concentus (already a dependency)
- WAV as lossless option (simple, already supported by NAudio)
- Resample from 24kHz (Pocket TTS native) to standard rates if needed (44.1kHz for MP3)

## Acceptance Criteria
- [ ] Can export to MP3 with acceptable quality
- [ ] Can export to OGG/Opus
- [ ] Can export to WAV (lossless)
- [ ] Save dialog with format picker and file name
- [ ] Exported files play correctly in standard media players

## Work Log
<!-- Appended by /work during execution -->
