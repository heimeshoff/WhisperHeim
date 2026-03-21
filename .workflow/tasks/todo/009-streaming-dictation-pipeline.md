# Task: Streaming Dictation Pipeline

**ID:** 009
**Milestone:** M1 - Live Dictation + Core App
**Size:** Large
**Created:** 2026-03-21
**Dependencies:** 007-silero-vad, 008-parakeet-asr

## Objective
Wire VAD and ASR into a streaming pipeline that produces live text from continuous speech.

## Details
Create a DictationPipeline that orchestrates: AudioCapture -> VAD -> ASR -> Text output. When VAD detects speech start, begin accumulating. On speech end, send to ASR. For live partial results: also send accumulated audio every ~1-2 seconds during ongoing speech (tumbling window approach). Diff current transcription against previous to determine new text. Handle rapid speech/pause cycles. Pipeline should be startable/stoppable. Expose events: PartialResult(string text), FinalResult(string text). Thread-safe throughout.

## Acceptance Criteria
- [ ] Continuous speech produces streaming text updates
- [ ] Pauses finalize segments
- [ ] Latency under 2s
- [ ] Handles rapid start/stop
- [ ] No audio glitches or dropped segments

## Notes
Tumbling window approach for partial results every ~1-2 seconds. Diff-based new text detection.

## Work Log
<!-- Appended by /work during execution -->
