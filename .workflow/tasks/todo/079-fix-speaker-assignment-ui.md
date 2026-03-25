# Task: Fix Speaker Assignment UI -- ComboBox Bug + Per-Segment Reassignment

**ID:** 079
**Milestone:** M2 - Audio Capture + Call Transcription
**Size:** Medium
**Created:** 2026-03-25
**Dependencies:** 077

## Objective
Fix the broken speaker assignment combo boxes and enable flexible per-segment speaker reassignment in the transcript detail view.

## Details

### Bug Fix: ComboBox Closes Immediately
The speaker ComboBox in the transcript detail drawer closes immediately when clicked. Root cause: the ComboBox is nested inside a Border with a `MouseLeftButtonDown` handler (`Segment_MouseLeftButtonDown`) that fires audio playback. The click event bubbles from the ComboBox up to the Border, stealing the click.

**Fix:** Prevent the click event from bubbling when interacting with the ComboBox. Add a `PreviewMouseLeftButtonDown` handler on the ComboBox (or its container) that marks the event as handled when the ComboBox is in editing/dropdown mode.

### Per-Segment Speaker Reassignment
- Initial speaker assignment: names mapped to cluster IDs by order of first appearance (from task 077)
- Any individual transcript segment should be reassignable to a different speaker
- The ComboBox dropdown should list all available speaker names (from the session's speaker list + any auto-detected cluster IDs)
- Changing a segment's speaker should persist immediately (update the transcript JSON)
- Consider: "Apply to all segments with this speaker ID" option -- if the user reassigns one segment from "Speaker 1" to "Alice", offer to reassign all "Speaker 1" segments to "Alice"

### Speaker Name List
- The available speaker names come from: (a) names provided before/during recording (task 076), (b) auto-detected cluster IDs as fallback ("Speaker 1", "Speaker 2", etc.)
- User should be able to add new speaker names after transcription (e.g., they forgot to add someone before recording)
- Editing a speaker name in the header should propagate to all segments attributed to that speaker

## Acceptance Criteria
- [ ] Speaker ComboBox opens and stays open when clicked (bug fixed)
- [ ] Clicking a ComboBox does not trigger audio playback
- [ ] Any segment's speaker can be reassigned via the ComboBox dropdown
- [ ] Speaker reassignment persists to transcript JSON
- [ ] "Apply to all" option when reassigning a speaker (bulk update)
- [ ] New speaker names can be added after transcription
- [ ] Editing a speaker name in the header updates all segments with that speaker

## Notes
- Bug location: `TranscriptsPage.xaml` lines 493-520, handler at `TranscriptsPage.xaml.cs` line 594
- The `Segment_MouseLeftButtonDown` handler on the parent Border intercepts clicks meant for the ComboBox
- Research file: `.workflow/research/transcription-engine-overhaul.md`

## Work Log
<!-- Appended by /work during execution -->
