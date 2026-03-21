# Task: Speaker Diarization with sherpa-onnx

**ID:** 017
**Milestone:** M2 - Audio Capture + Call Transcription
**Size:** Large
**Created:** 2026-03-21
**Dependencies:** 005

## Objective
Identify and separate speakers in an audio recording using sherpa-onnx diarization.

## Details
Use sherpa-onnx's speaker diarization API with pyannote segmentation 3.0 ONNX models. Download diarization models via the model manager (005). Accept a WAV file as input. Output a list of segments: {speakerId, startTime, endTime}. For call transcription, use the dual streams to help attribution: mic audio = user, loopback audio = other speakers. If single-stream, rely purely on diarization. Run as a background task (can take time for long recordings).

## Acceptance Criteria
- [ ] Correctly separates 2+ speakers
- [ ] Segments have accurate timestamps
- [ ] Models download automatically via model manager
- [ ] Handles long recordings (1hr+)
- [ ] Runs as a background task without blocking UI

## Notes
Uses pyannote segmentation 3.0 ONNX models through sherpa-onnx. Dual-stream hint from call recording improves attribution accuracy.

## Work Log
<!-- Appended by /work during execution -->
