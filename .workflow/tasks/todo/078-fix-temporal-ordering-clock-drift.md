# Task: Fix Temporal Ordering -- Clock Drift Correction

**ID:** 078
**Milestone:** M2 - Audio Capture + Call Transcription
**Size:** Small
**Created:** 2026-03-25
**Dependencies:** 077

## Objective
Fix incorrect temporal ordering of transcript segments from mic and loopback streams by applying clock drift correction before merging.

## Details

### The Problem
WASAPI mic capture and loopback capture use different hardware clocks (ADC vs DAC). These clocks drift at approximately 0.1% (1ms per second), causing:
- 30-minute recording: ~1.8 seconds drift
- 60-minute recording: ~3.6 seconds drift

This causes transcript segments to appear out of order -- e.g., a remote speaker's segment appears before the local speaker's response even though the response came first.

### Linear Drift Correction
After recording stops, before merging segments:
1. Measure the actual sample count / duration of each WAV file
2. If durations differ, compute a drift factor: `correction = mic_duration / loopback_duration`
3. Scale all loopback segment timestamps (start and end) by this factor
4. Then interleave mic + loopback segments and sort by corrected start time

### Implementation
- Add drift correction step in `CallTranscriptionPipeline.ProcessAsync`, after diarization/VAD and before the merge-and-sort step
- Log the measured drift for diagnostics (e.g., "Clock drift: 1.2s over 30min recording")
- For short recordings (<5 min), drift is negligible -- still apply correction but expect near-zero adjustment

## Acceptance Criteria
- [ ] WAV file durations measured after recording
- [ ] Loopback segment timestamps scaled by drift correction factor
- [ ] Segments from both streams interleaved in correct temporal order
- [ ] Drift amount logged for diagnostics
- [ ] Short recordings still produce correctly ordered output
- [ ] Long recordings (30min+) no longer have out-of-order segments

## Notes
- This is a small, focused change to the pipeline merge step
- The correction is linear (constant drift rate assumption) -- good enough for WASAPI
- Research file: `.workflow/research/transcription-engine-overhaul.md`, section "Dual-Stream Temporal Ordering"

## Work Log
<!-- Appended by /work during execution -->
