# Task: Add proper play icon to "Open in Player" button

**ID:** 090
**Size:** Small
**Created:** 2026-03-26
**Dependencies:** None

## Objective

Replace the raw Unicode glyph on the "Open in Player" button with a proper Wpf.Ui `SymbolIcon`, matching the pattern used by every other button in the transcript view.

## Details

The `OpenExternalButton` in `TranscriptsPage.xaml` (line 684-691) uses `Content="&#xE768;  Open in Player"` — a raw Unicode character that doesn't render as a proper icon. All other buttons use `ui:SymbolIcon` elements.

### Fix

Replace the plain text `Content` with a `StackPanel` containing a `ui:SymbolIcon` (e.g., `Play24` or `Open24`) and a `TextBlock`, following the same pattern as the other buttons in the file.

## Acceptance Criteria

- [ ] "Open in Player" button shows a recognizable play icon
- [ ] Icon style matches other buttons in the transcript view
