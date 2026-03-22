# Task: Reduce Transcripts List Column Width

**ID:** 053
**Milestone:** --
**Size:** Small
**Created:** 2026-03-22
**Dependencies:** --

## Objective
Shrink the transcripts list column on the recordings page so content is readable at smaller window sizes.

## Details
The transcripts list uses a fixed width that takes too much space when the window is narrow. The actual transcript content becomes almost unreadable. Reduce the list width or make the split responsive/resizable so content always has adequate room.

## Acceptance Criteria
- [x] Transcript list column is narrower, leaving more room for content
- [x] Transcript content remains readable at half-screen window width
- [x] List items remain readable in the narrower column

## Work Log
<!-- Appended by /work during execution -->
### 2026-03-22
- Reduced transcript list column width from fixed 280px to 200px default
- Added MinWidth="160" and MaxWidth="280" constraints for responsive behavior
- Build verified successfully
