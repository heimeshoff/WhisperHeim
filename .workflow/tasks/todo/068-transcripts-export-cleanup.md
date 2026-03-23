# Task 068: Transcripts Page Export Cleanup

**Status:** Todo
**Size:** Small
**Milestone:** UI Polish (Quiet Engine)
**Dependencies:** None

## Description

Streamline the export options on the transcripts/recordings page:

1. **Remove the TXT download button** — only MD and JSON downloads are needed.
2. **Copy button copies Markdown** — change `CopyToClipboard_Click` to use `FormatAsMarkdown()` instead of `FormatAsPlainText()`.
3. **Brief "Copied!" tooltip** — show a short visual confirmation when the copy succeeds (e.g., a tooltip or transient text near the button that fades after ~1–2 seconds).

## Acceptance Criteria

- [ ] TXT button is removed from the action panel in `TranscriptsPage.xaml`
- [ ] `ExportText_Click` handler can be removed from code-behind
- [ ] Copy button calls `FormatAsMarkdown()` and copies the result to clipboard
- [ ] A brief "Copied!" indicator appears after clicking Copy and disappears automatically
- [ ] MD and JSON download buttons remain unchanged

## Files to Modify

- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` (remove TXT button)
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` (change copy format, add tooltip feedback, optionally remove `ExportText_Click`)
