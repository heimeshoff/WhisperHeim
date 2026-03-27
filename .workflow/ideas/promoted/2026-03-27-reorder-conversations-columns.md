> **Promoted to task:** 091-reorder-conversations-columns.md on 2026-03-27

# Idea: Reorder and resize conversation list columns

**Captured:** 2026-03-27
**Source:** User input
**Status:** Raw
**Last Refined:** --

## Description
Change the column order and sizing in the Conversations tab list:

1. **Column order:** Title → Speakers → Time (currently: Title → Time → Speakers)
2. **Title column width:** Auto-size to fit the longest title, plus a small gap — no wasted space
3. **Speakers column:** Appears right after the title gap
4. **Time column:** Right-aligned, pushed to the right boundary

## Initial Thoughts
- This is a UI layout change in the TranscriptsPage (or equivalent conversations list view)
- Likely involves reordering XAML column definitions and adjusting width properties (Auto vs * vs fixed)

## Open Questions
- Which XAML file defines the conversations list columns?

## Refinement Log
