# Task: Transcript Audio Playback

**ID:** 038
**Milestone:** M2 - Audio Capture + Call Transcription
**Size:** Large
**Created:** 2026-03-21
**Dependencies:** 036

## Objective
Enable audio playback from the transcript viewer — click on a transcript segment to play audio from that point. Preserve audio files alongside transcripts for long-term access.

## Details

### Audio Preservation
After transcription completes, copy the source WAV files (mic + loopback) to the transcripts directory alongside the JSON file. Store the audio file paths in the transcript JSON so the viewer can locate them. Consider merging mic + loopback into a single stereo or mixed-down WAV for simpler playback, or keep them separate and mix at playback time.

### Playback UI
Add a play button or click handler on each transcript segment. Clicking a segment starts playback from that segment's `StartTime`. Use NAudio (already a dependency) for audio playback — `WaveOutEvent` or `DirectSoundOut` with `AudioFileReader`. Add basic playback controls: play/pause button, current position indicator. Highlight the currently-playing segment in the transcript view. Auto-scroll to follow playback if desired.

### Segment Click Behavior
- Click on a segment → start playback from `segment.StartTime`
- Playback continues through subsequent segments until paused or end of file
- Visual indication of which segment is currently playing

## Acceptance Criteria
- [ ] Audio files (WAV) preserved alongside transcript JSON after recording
- [ ] Audio file paths stored in transcript JSON
- [ ] Clicking a segment starts audio playback from that segment's start time
- [ ] Play/pause control available
- [ ] Currently-playing segment visually highlighted
- [ ] Playback works for both call recordings and file transcriptions (where source audio exists)
- [ ] Old transcripts without audio files gracefully show no playback option

## Notes
NAudio is already used for audio capture and resampling. For file transcriptions (drag-drop), the source file path is already known and could be referenced directly. For call recordings, the temp WAV files need to be copied before the temp directory is cleaned up.

## Work Log
<!-- Appended by /work during execution -->
