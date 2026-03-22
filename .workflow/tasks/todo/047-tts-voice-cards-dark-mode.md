# Task: Fix TTS Voice Cards Dark Mode Background

**ID:** 047
**Milestone:** --
**Size:** Small
**Created:** 2026-03-22
**Dependencies:** --

## Objective
Replace hardcoded white background on library voice cards in the Text to Speech page with theme-aware brushes so they look correct in dark mode.

## Details
The voice cards are built programmatically in `TextToSpeechPage.xaml.cs` (~line 665) using `Color.FromRgb(0xFF, 0xFF, 0xFF)` (hardcoded white). This ignores the active theme, so in dark mode the cards appear as bright white boxes while everything else adapts.

Fix: replace the hardcoded white with the dynamic theme brush `CardBackgroundFillColorDefaultBrush` (already used elsewhere on the same page). Also check the delete button foreground color (`Color.FromRgb(0xBA, 0x1A, 0x1A)`) for theme consistency.

Ideally, consider moving the voice card creation from code-behind to a XAML DataTemplate with proper `DynamicResource` bindings, but the minimal fix is just swapping the brush.

Key file:
- `src/WhisperHeim/Views/Pages/TextToSpeechPage.xaml.cs` — voice card Border creation

## Acceptance Criteria
- [ ] Voice cards use theme-aware background brush instead of hardcoded white
- [ ] Cards look correct in both Light and Dark themes
- [ ] Text on cards remains readable in both themes

## Notes
The rest of the page already uses DynamicResource brushes correctly — this is just a missed spot in code-behind.

## Work Log
<!-- Appended by /work during execution -->
