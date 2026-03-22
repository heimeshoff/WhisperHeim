# Task: Consistent Max-Width Across All Pages

**ID:** 045
**Milestone:** --
**Size:** Small
**Created:** 2026-03-22
**Dependencies:** --

## Objective
Apply the same content max-width constraint used on the Transcripts page to all other pages for visual consistency.

## Details
The Transcripts page already constrains its content to a maximum width. Other pages (Recordings/TranscribeFiles, Templates, Text to Speech, Dictation, About, General Settings) stretch full width. Apply the same max-width approach from Transcripts to all pages so content doesn't stretch uncomfortably wide on larger screens.

Key files:
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` — reference implementation
- `src/WhisperHeim/Views/Pages/TranscribeFilesPage.xaml`
- `src/WhisperHeim/Views/Pages/TemplatesPage.xaml`
- `src/WhisperHeim/Views/Pages/TextToSpeechPage.xaml`
- `src/WhisperHeim/Views/Pages/DictationPage.xaml`
- `src/WhisperHeim/Views/Pages/AboutPage.xaml`
- `src/WhisperHeim/Views/Pages/GeneralPage.xaml`

## Acceptance Criteria
- [ ] All pages use the same max-width as the Transcripts page
- [ ] Content is centered (not left-aligned) when the window is wider than the max-width
- [ ] No visual regressions on pages that already look correct

## Notes
Small task — copy the existing pattern from TranscriptsPage to the other pages.

## Work Log
<!-- Appended by /work during execution -->
