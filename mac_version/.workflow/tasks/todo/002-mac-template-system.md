# Task 002: Mac Template System — Voice-Triggered Text Expansion

**Status:** Todo
**Size:** Medium
**Created:** 2026-04-19
**Milestone:** MVP

## Description

Add the template system: a second hotkey triggers voice capture, the spoken text is fuzzy-matched against template names, and the matched template's expanded text is typed into the active app. Includes a small GUI for managing templates.

## Architecture

- **Second hotkey:** Separate pynput hold-to-talk binding (e.g., Cmd+Option)
- **Fuzzy matching:** Port Levenshtein distance matcher from Windows WhisperHeim (FuzzyMatcher.cs)
- **Placeholder expansion:** Support `{date}` and `{time}` placeholders (port from TemplatePlaceholderExpander.cs)
- **Template storage:** JSON in settings file, same structure as Windows version
- **Template editor GUI:** PyObjC native window or tkinter

## Subtasks

- [ ] Template data model (name, text, group — matching Windows TemplateItem structure)
- [ ] Fuzzy matcher (Levenshtein distance + containment check, ported from FuzzyMatcher.cs)
- [ ] Placeholder expander ({date}, {time})
- [ ] Template orchestrator (hold hotkey → capture → transcribe → fuzzy match → expand → type)
- [ ] Template editor GUI (list templates, add/edit/delete, group management)
- [ ] Template editor accessible from menu bar icon
- [ ] System template: "Repeat" (re-type last dictation)

## Acceptance Criteria

- User holds template hotkey, says a template name, releases — matched template text appears at cursor
- If no match found, nothing is typed (or optionally the raw transcription)
- Fuzzy matching tolerates minor speech recognition errors (threshold ~0.4)
- Template editor allows CRUD operations on templates
- Templates persist across app restarts
- {date} expands to YYYY-MM-DD, {time} expands to HH:MM

## Dependencies

- Task 001 (dictation core — transcription service, audio capture, hotkey service, text insertion)

## Technical Notes

- Windows version's FuzzyMatcher uses Levenshtein distance with 0.4 threshold, plus containment bonus (0.95 score)
- Template hotkey should have a ~4 second recording timeout (matches Windows behavior)
- Consider visual feedback in menu bar during template matching
