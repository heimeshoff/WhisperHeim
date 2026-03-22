# Task: Rename Export Button to "MD"

**ID:** 055
**Milestone:** --
**Size:** Small
**Created:** 2026-03-22
**Dependencies:** --

## Objective
Rename the "Export" button on the recordings page to "MD" for consistency with the Text and JSON export buttons.

## Details
There are three export buttons: Text, JSON, and one labeled "Export" (which exports Markdown). Rename "Export" to "MD" so all three follow the same naming pattern.

## Acceptance Criteria
- [x] Button reads "MD" instead of "Export"
- [x] Consistent naming: Text, JSON, MD

## Work Log
<!-- Appended by /work during execution -->
- 2026-03-22: Changed `Text="EXPORT"` to `Text="MD"` in TranscriptsPage.xaml (line 376). The three export buttons now read: MD, JSON, TXT consistently.
