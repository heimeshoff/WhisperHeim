# Task: Remove Magic Replace from Edit Template

**ID:** 052
**Milestone:** --
**Size:** Small
**Created:** 2026-03-22
**Dependencies:** --

## Objective
Remove the magic replace feature from the edit template page.

## Details
The magic replace functionality in the template editor is not wanted. Remove the magic replace UI and any clipboard-related behavior it introduces.

## Acceptance Criteria
- [x] Magic replace button/UI is removed from the edit template page
- [x] No clipboard side effects from magic replace remain
- [x] Template editing otherwise works as before

## Work Log
<!-- Appended by /work during execution -->
### 2026-03-22
- Removed "Magic Replacement" info card from TemplatesPage.xaml (bottom bento grid replaced with just the New Template card)
- Removed `{clipboard}` placeholder pill from the template editor header
- Updated placeholder text hint to remove `{clipboard}` reference
- Removed `{clipboard}` expansion logic and `GetClipboardText()` method from `TemplatePlaceholderExpander.cs`
- Removed `using System.Windows` import (no longer needed without Clipboard usage)
- Build verified: no errors in changed files (pre-existing errors in unrelated files only)
