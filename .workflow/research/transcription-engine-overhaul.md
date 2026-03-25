# Research: Transcription Engine Overhaul

**Date:** 2026-03-25
**Status:** Complete
**Relevance:** Milestone 2 (Call Transcription) -- diarization accuracy, dual-stream merging, stability, and queue architecture

## Summary

The current call transcription pipeline suffers from four interconnected problems: speaker over-segmentation (detecting 10+ speakers instead of 2-3), incorrect temporal ordering when merging mic and loopback streams, crashes during long recordings (30min+), and a blocking modal progress dialog with no queue support.

The single highest-impact fix is to **stop running full diarization on dual-stream recordings** and instead use Voice Activity Detection (VAD) per stream -- mic is always "You", loopback is always "Remote Speaker." This eliminates over-segmentation, reduces processing time, uses less memory, and solves the ordering problem at its root. For group calls, diarization should only run on the loopback stream with the expected speaker count explicitly set.

The queue architecture should replace the modal `TranscriptionProgressDialog` with an inline bottom-bar that expands into a full queue view, processing recordings sequentially with per-item status tracking.

## Key Findings

### 1. Speaker Over-Segmentation: Root Cause and Fix

**Root cause:** The clustering threshold in sherpa-onnx defaults to `0.5f` (in `SpeakerDiarizationService.cs:24`). A lower threshold = more clusters = more detected speakers. Additionally, chunked diarization (5-min chunks) assigns speaker IDs independently per chunk -- "Speaker 0" in chunk 1 may be a different person than "Speaker 0" in chunk 2, inflating the apparent speaker count.

**Key principle:** Mic stream is always a single known speaker (the user) -- never needs diarization. Loopback stream may contain 1 or more remote speakers -- needs diarization when multiple.

**Unified approach -- always diarize loopback, never diarize mic:**

| Scenario | Mic Stream | Loopback Stream |
|----------|-----------|----------------|
| 1-on-1 call (1 remote name) | VAD only → user's name | Diarize with `NumClusters=1` → remote name |
| Group call (N names provided) | VAD only → user's name | Diarize with `NumClusters=N` → assign names post-hoc |
| Group call (no names) | VAD only → user's name | Diarize with threshold `0.75-0.85`, auto-detect count |

**User provides speaker names before transcription starts.** The name list length implicitly sets `NumClusters` for the loopback stream. When `NumClusters` is set to a positive value, the clustering threshold is **ignored entirely** -- this is the single biggest fix for over-segmentation.

**Speaker name assignment after diarization:**
The diarizer outputs anonymous cluster IDs (Speaker 0, Speaker 1, ...) -- it doesn't know which voice belongs to which name. Three options:

1. **Manual post-transcription assignment (recommended)** -- Show the transcript with "Speaker 1", "Speaker 2" labels. Let the user click a speaker label to assign a name from the provided list. This is what Otter.ai and Descript do -- most practical and reliable.
2. **Order-of-first-appearance** -- Map Speaker 0 to first name in list, Speaker 1 to second, etc. Simple but fragile (first detected speaker depends on who talks first in each chunk).
3. **Voice enrollment** -- Pre-record a short sample of each speaker, compare embeddings at runtime. Accurate but complex UX overhead.

Option 1 is the clear winner. The user already provides names before transcription; after transcription completes, they review the transcript and assign names to the anonymous speaker IDs. This can be a one-click action per speaker.

**When only 1 remote speaker:** `NumClusters=1` on loopback means all loopback speech is attributed to that single remote speaker. Effectively equivalent to VAD-only but uses the same code path as group calls, keeping the pipeline simple.

**Fallback when no names provided:** Use constrained diarization with raised threshold (`0.75-0.85` instead of `0.5`). The user can still assign names after the fact.

**Cross-chunk speaker consistency for long group calls:**
When recordings are chunked (5-min chunks), each chunk assigns speaker IDs independently. For group calls this causes "Speaker 0" in chunk 1 to potentially be "Speaker 1" in chunk 2. Solutions:
- Extract a representative speaker embedding per cluster per chunk
- Compare embeddings across chunks using cosine similarity (threshold ~0.7)
- Build a global speaker ID mapping before assembling the final transcript
- This is only needed for group calls; for 1-on-1 calls with `NumClusters=1` on loopback, there's only one cluster per chunk so stitching is trivial

**Sources:**
- [sherpa-onnx C API: NumClusters overrides Threshold](https://github.com/k2-fsa/sherpa-onnx/blob/master/sherpa-onnx/c-api/c-api.h)
- [pyannote speaker-diarization-3.1](https://huggingface.co/pyannote/speaker-diarization-3.1)
- [WhisperX over-segmentation issue #804](https://github.com/m-bain/whisperX/issues/804)
- [NeMo MSDD: 60% DER reduction for 2-speaker](https://arxiv.org/pdf/2203.15974)

### 2. Dual-Stream Temporal Ordering

**Current problem:** Mic and loopback are diarized independently, then merged and sorted by start time. But the diarization segments from each stream may not align correctly because:
1. Clock drift between WASAPI capture devices (~0.1%, or ~1 second per 16 minutes)
2. Independent chunking boundaries produce segments with slightly different start times

**Clock drift magnitude:**
- 30-minute recording: ~1.8 seconds drift
- 60-minute recording: ~3.6 seconds drift
- Both streams share the same `StartTimestamp` but hardware clocks diverge over time

**Recommended fix -- linear drift correction:**
1. After recording, measure the actual duration of each WAV file
2. If durations differ, compute a drift factor: `correction = mic_duration / loopback_duration`
3. Scale all loopback segment timestamps by this factor
4. Then interleave and sort by corrected start time

**With VAD-only approach (Approach A), ordering is simpler:**
- VAD segments from each stream have timestamps relative to each stream's start
- Apply drift correction, then interleave
- No clustering means no chunk-boundary artifacts

**Sources:**
- [Deepgram: Multichannel vs Diarization](https://deepgram.com/learn/multichannel-vs-diarization)
- [AssemblyAI: multichannel and speaker_labels cannot coexist](https://www.assemblyai.com/blog/multichannel-speaker-diarization)
- [WASAPI clock drift (Mozilla bug)](https://bugzilla.mozilla.org/show_bug.cgi?id=1295193)

### 3. Long Recording Stability

**Current state:** The pipeline already has good foundations -- out-of-process diarization worker, 5-minute chunking, GC pinning. But crashes still occur.

**Identified risks and mitigations:**

| Risk | Current State | Recommended Fix |
|------|--------------|----------------|
| ONNX memory leak across chunks | Singleton `_diarizer` reused | Create fresh instance per chunk (worker already does this) |
| Native crash kills UI | Out-of-process worker exists | **Always** use OOP worker for recordings > 5 min |
| No GC between chunks | Manual `GC.Collect()` absent | Add `GC.Collect()` + `WaitForPendingFinalizers()` between chunks |
| Temp files not cleaned on crash | Only cleaned in `finally` | Add cleanup on app startup for stale temp files |
| Full audio array in memory | Chunks loaded individually | Already good -- maintain this pattern |
| No timeout on individual segments | 2-min timeout per chunk exists | Already good -- maintain this pattern |

**If switching to VAD-only (Approach A), most stability issues vanish:**
- VAD is lightweight (~1MB model, <1ms per frame)
- No embedding model loaded (~38MB saved)
- No clustering computation
- Processing time drops dramatically
- Memory footprint stays flat regardless of recording length

**Sources:**
- [OnnxRuntime memory leak #14466](https://github.com/microsoft/onnxruntime/issues/14466)
- [OnnxRuntime repeated inference leak #22271](https://github.com/microsoft/onnxruntime/issues/22271)
- [MeetStream: Processing Long Meeting Audio](https://blog.meetstream.ai/tutorials/how-to-process-long-meeting-audio/)

### 4. Transcription Queue Architecture

**Current UX problem:** `TranscriptionProgressDialog` is a modal window that blocks the entire UI during transcription. No queue exists -- `TranscriptionBusyService` rejects concurrent requests with a boolean guard.

**Recommended architecture:**

```
┌─────────────────────────────────────┐
│  Main Window (Templates/Transcripts)│
│                                     │
│         [normal content]            │
│                                     │
├─────────────────────────────────────┤
│ ▶ Transcribing "Call 14:30" (47%)   │  ← collapsed bottom bar
└─────────────────────────────────────┘

         click to expand ↓

┌─────────────────────────────────────┐
│  Transcription Queue                │
├─────────────────────────────────────┤
│ ● Call 14:30    Transcribing  [===  ] 47%  │
│ ○ Call 11:15    Queued                     │
│ ○ meeting.mp3   Queued                     │
│ ✓ Call 09:00    Completed     2 min ago    │
│ ✗ voice.ogg     Failed        retry ↻     │
└─────────────────────────────────────┘
```

**Queue item states:** Queued → Loading → Diarizing → Transcribing → Assembling → Completed | Failed

**Key design decisions:**
- **Non-modal**: Replace modal dialog with a persistent bottom bar (like VS Code terminal panel)
- **FIFO queue**: New items append to end; active item at top
- **Auto-enqueue**: Recordings auto-enqueue on stop; file imports auto-enqueue on selection
- **Background processing**: Queue processes items sequentially on a background thread
- **Step progress**: Active item shows current pipeline stage + sub-progress
- **Retry**: Failed items can be retried (re-enqueue at end)
- **Cancel**: Active item can be cancelled; queued items can be removed

**Existing apps for reference:**
- **Buzz Transcriber**: Per-file progress bars, tabs for each transcription, individual cancel
- **Descript**: Loading state on document, sequential batch processing
- **MacWhisper**: Simple single-file progress indicator

**Sources:**
- [Syncfusion WPF Step ProgressBar](https://www.syncfusion.com/blogs/post/new-wpf-step-progressbar-to-track-multi-step-process)
- [Buzz Transcriber](https://chidiwilliams.github.io/buzz/docs)

## Implications for This Project

1. **Mic: VAD-only, always.** The mic stream is always a single known speaker (the user). Skip diarization entirely, use VAD to detect speech segments, label them with the user's name.

2. **Loopback: always diarize, but constrained.** User provides remote speaker names before transcription. The name count sets `NumClusters`, which eliminates over-segmentation. After transcription, user assigns names to anonymous cluster IDs (one-click per speaker in the transcript view).

3. **Queue-first architecture**: The transcription engine should be restructured around a persistent queue service. All entry points (recording stop, file import, manual trigger) enqueue work items. Processing is sequential and non-blocking.

4. **Auto-transcribe on recording stop**: When a recording finishes, prompt for remote speaker names (pre-filled from the recording session), then auto-enqueue for transcription.

5. **File transcription**: Importing audio files should prompt for expected speaker count/names, then enqueue. Single-stream files need full diarization with `NumClusters` from user input.

6. **Linear drift correction**: Compare WAV durations after recording, apply proportional timestamp correction to loopback segments before merging with mic segments.

7. **Cross-chunk speaker stitching for group calls**: Use speaker embeddings to maintain consistent IDs across 5-minute processing chunks. Not needed for 1-on-1 calls where `NumClusters=1`.

## Open Questions

- Should the queue persist across app restarts (e.g., if the app crashes mid-transcription)?
- Should there be a limit on queue depth or total pending audio duration?
- Should completed queue items be kept indefinitely or pruned after N days?
- How should the speaker-name-to-cluster-ID assignment UX work? (Dropdown per speaker label? Click-to-cycle through names? Drag-and-drop?)

## Sources

- [sherpa-onnx C API](https://github.com/k2-fsa/sherpa-onnx/blob/master/sherpa-onnx/c-api/c-api.h)
- [Deepgram: Multichannel vs Diarization](https://deepgram.com/learn/multichannel-vs-diarization)
- [AssemblyAI: Multichannel and Speaker Diarization](https://www.assemblyai.com/blog/multichannel-speaker-diarization)
- [pyannote speaker-diarization-3.1](https://huggingface.co/pyannote/speaker-diarization-3.1)
- [NeMo MSDD paper](https://arxiv.org/pdf/2203.15974)
- [WhisperX over-segmentation #804](https://github.com/m-bain/whisperX/issues/804)
- [OnnxRuntime memory issues #14466, #22271](https://github.com/microsoft/onnxruntime/issues/14466)
- [MeetStream: Long Meeting Audio](https://blog.meetstream.ai/tutorials/how-to-process-long-meeting-audio/)
- [WASAPI clock drift (Mozilla)](https://bugzilla.mozilla.org/show_bug.cgi?id=1295193)
- [Syncfusion WPF Step ProgressBar](https://www.syncfusion.com/blogs/post/new-wpf-step-progressbar-to-track-multi-step-process)
- [Buzz Transcriber](https://chidiwilliams.github.io/buzz/docs)
- [Brasstranscripts: Diarization Models Comparison](https://brasstranscripts.com/blog/speaker-diarization-models-comparison)
