# Task: Template System

**ID:** 013
**Milestone:** M1 - Live Dictation + Core App
**Size:** Medium
**Created:** 2026-03-21
**Dependencies:** 011-end-to-end-dictation

## Objective
Define named text templates triggered by a dedicated hotkey + voice keyword.

## Details
Templates stored in settings.json as a list of {name, text} pairs. Dedicated template hotkey (default: Alt+Win). Workflow: press template hotkey -> say template name -> template text is inserted at cursor. Use ASR to transcribe the spoken name, fuzzy-match against template names. Template text supports multi-line and basic placeholders ({date}, {time}, {clipboard}). Settings UI page for managing templates (add, edit, delete). Show a small toast/notification confirming which template was matched.

## Acceptance Criteria
- [ ] Template triggered by hotkey+voice
- [ ] Correct template inserted
- [ ] Fuzzy matching works for approximate names
- [ ] Templates manageable in settings UI
- [ ] Confirmation shown

## Notes
Fuzzy matching for voice-to-template-name. Placeholders: {date}, {time}, {clipboard}. Default hotkey: Alt+Win.

## Work Log
<!-- Appended by /work during execution -->
