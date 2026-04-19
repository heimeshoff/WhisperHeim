# Task 002: Mac Template System — Voice-Triggered Text Expansion

**Status:** Done
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

- [x] Template data model (name, text, group — matching Windows TemplateItem structure)
- [x] Fuzzy matcher (Levenshtein distance + containment check, ported from FuzzyMatcher.cs)
- [x] Placeholder expander ({date}, {time})
- [x] Template orchestrator (hold hotkey → capture → transcribe → fuzzy match → expand → type)
- [x] Template editor GUI (list templates, add/edit/delete, group management)
- [x] Template editor accessible from menu bar icon
- [x] System template: "Repeat" (re-type last dictation)

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

## Work Log

### 2026-04-19 — Implementation Complete

Ported the full template system from Windows WhisperHeim to macOS:

**New files created:**
- `whisperheim/services/templates/__init__.py` — package init
- `whisperheim/services/templates/fuzzy_matcher.py` — Levenshtein distance matcher (threshold 0.4, containment bonus 0.95), ported from FuzzyMatcher.cs
- `whisperheim/services/templates/placeholder_expander.py` — {date}/{time} expansion, ported from TemplatePlaceholderExpander.cs
- `whisperheim/services/templates/template_service.py` — template CRUD, fuzzy matching, system templates (Repeat), ported from TemplateService.cs
- `whisperheim/services/templates/template_orchestrator.py` — hotkey→capture→match→type workflow with 4s timeout, ported from TemplateOrchestrator.cs
- `whisperheim/services/templates/template_editor.py` — tkinter GUI for template management (list, add, edit, delete, groups)
- `tests/__init__.py` — test package
- `tests/test_fuzzy_matcher.py` — 20 tests for fuzzy matcher and placeholder expander
- `tests/test_template_service.py` — 18 tests for template service CRUD, matching, and groups

**Modified files:**
- `whisperheim/services/settings_service.py` — added TemplateItem, TemplateGroup, TemplateSettings dataclasses; template_hotkey (Cmd+Alt+Shift); template settings loading/saving
- `whisperheim/app.py` — integrated template system: "Edit Templates..." menu item, template orchestrator startup, last dictation tracking for Repeat, shutdown cleanup

All 38 tests pass.
