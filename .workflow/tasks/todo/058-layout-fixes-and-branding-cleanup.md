# Task: Layout Fixes and Branding Cleanup

**ID:** 058
**Milestone:** --
**Size:** Small
**Created:** 2026-03-22
**Dependencies:** --

## Objective
Fix layout width issues on the Transcripts and TTS pages, fix transcript card overflow, and remove the "LOCAL-FIRST AI" subtitle from the sidebar.

## Details

### 1. Transcript List Cards Overflow
**File:** `TranscriptsPage.xaml` (line ~107)
The transcript list column is `Width="200" MinWidth="160" MaxWidth="280"`, but the card content (name + date + preview) can overflow the column width since there's no width constraint on the card items themselves. Cards should clip/ellipsis within their container, not overflow.

### 2. Transcripts Page Crunched When No Selection
**File:** `TranscriptsPage.xaml` (line ~51)
The outer grid uses `MaxWidth="900" HorizontalAlignment="Center"`. When no transcript is selected, the right viewer panel has minimal content (just a placeholder text), causing the grid to shrink below 900px. The page should always stretch to fill available width (up to MaxWidth), regardless of whether a transcript is selected. The layout should only shrink when the window itself is smaller than the max width.

### 3. TTS Page Width Not Filling Available Space
**File:** `TextToSpeechPage.xaml` (line ~77)
Same pattern: `MaxWidth="900" HorizontalAlignment="Center"`. When the text input field is empty, the page uses less width than available. Should always stretch to fill available space up to the max width.

### 4. Remove "LOCAL-FIRST AI" Subtitle
**File:** `MainWindow.xaml` (line ~133)
Remove the `BrandingSubtitle` TextBlock that shows "LOCAL-FIRST AI" below the logo in the sidebar.

## Acceptance Criteria
- [ ] Transcript list cards never overflow their column — text truncates with ellipsis
- [ ] Transcripts page always uses full available width (up to MaxWidth) even with no transcript selected
- [ ] TTS page always uses full available width (up to MaxWidth) even with empty input
- [ ] "LOCAL-FIRST AI" text removed from sidebar
- [ ] All pages look correct in both light and dark themes
- [ ] No regressions when resizing the window

## Work Log
<!-- Appended by /work during execution -->
