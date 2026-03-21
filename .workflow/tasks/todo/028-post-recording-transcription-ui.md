# Task: Post-recording transcription pipeline with progress UI

**ID:** 028
**Milestone:** M2 - Audio Capture + Call Transcription
**Size:** Medium
**Created:** 2026-03-21
**Dependencies:** 027-tray-menu-call-recording

## Objective
When call recording stops, the transcription pipeline runs automatically with a visible progress indicator, and the user is navigated to the finished transcript.

## Details

### Auto-trigger pipeline
- In `MainWindow`, subscribe to `CallRecordingService.RecordingStopped`
- On stop, call `CallTranscriptionPipeline.ProcessAsync(session, progress, cancellationToken)` on a background thread
- Pass an `IProgress<TranscriptionPipelineProgress>` to drive the progress UI

### Progress UI
- Show a modal or overlay dialog while the pipeline runs
- Display: current stage name (Loading Audio, Diarizing, Transcribing, Assembling, Saving), stage progress bar, overall progress bar
- Include a "Cancel" button wired to a `CancellationTokenSource`
- Pipeline stages have weighted progress: Load 5%, Diarize 30%, Transcribe 55%, Assemble 5%, Save 5%
- Close the dialog automatically when pipeline reaches `PipelineStage.Completed`

### Navigation to result
- After pipeline completes successfully, refresh `TranscriptsPage` (call `RefreshList()`)
- Navigate to "Transcripts" page via `NavigateTo("Transcripts")`
- Show the main window if it was hidden (tray-only mode)
- Optionally auto-select the new transcript in the list

### Error handling
- If pipeline throws, close the progress dialog and show an error message
- If cancelled, clean up gracefully (no partial transcript saved)

## Acceptance Criteria
- [ ] Pipeline auto-starts when recording stops
- [ ] Progress dialog shows current stage and percentage
- [ ] Cancel button aborts the pipeline
- [ ] On success: TranscriptsPage refreshed, navigated to, window shown
- [ ] On error: error message displayed, no crash
- [ ] On cancel: dialog closes cleanly, no partial transcript

## Notes
- `CallTranscriptionPipeline` is CPU-intensive (diarization + ASR) — must run on background thread, marshal progress to UI thread
- Consider using `Task.Run` with `IProgress<T>` pattern
- The progress dialog could be a simple `Window` with a `ProgressBar` and text labels — no need for a full page

## Work Log
<!-- Appended by /work during execution -->
