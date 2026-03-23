# Task 067: Dictation Page Responsive Layout & Card Alignment

**Status:** Todo
**Size:** Medium
**Milestone:** UI Polish (Quiet Engine)
**Dependencies:** None

## Description

The dictation page cards are not aligned consistently and the layout does not adapt to narrow window widths. This task fixes width alignment, adds responsive stacking, and swaps the hotkey row content order.

## Requirements

### 1. Card Width Alignment
- The bento grid row (dictation field + hotkeys card) must be exactly the same width as the audio input device card above — no overshoot on left or right.

### 2. Responsive Stacking (Wide → Narrow)
- **Wide mode:** Dictation field and hotkeys card sit side-by-side (current 8:4 ratio).
- **Narrow mode:** Cards stack vertically — Audio Input → Dictation → Hotkeys (top to bottom). All cards become full-width when stacked.
- **Breakpoint:** Trigger when the hotkeys card would start shrinking below its natural minimum content width.

### 3. Warning Card Responsive Behaviour
- **Wide mode:** When visible, the device warning card shares 50/50 horizontal space with the audio input device card.
- **Narrow mode:** Warning card stacks below the audio input device card, full-width.

### 4. Hotkey Row Content Swap
- In each hotkey shortcut row, move the keyboard shortcut pills to the **left** and the description text to the **right** (currently they are the opposite way around).

## Acceptance Criteria

- [ ] Bento grid (dictation + hotkeys) is exactly as wide as the audio input card
- [ ] At narrow widths, hotkeys card moves below dictation card (row → column)
- [ ] All cards are full-width when in stacked/column mode
- [ ] Warning card is 50/50 with audio input in wide mode, stacks below in narrow mode
- [ ] Hotkey rows show shortcut pills on the left, label text on the right
- [ ] Layout transitions feel smooth and natural
- [ ] No visual regressions on the dictation page at default (1200×800) window size

## Technical Notes

- File: `src/WhisperHeim/Views/Pages/DictationPage.xaml`
- WPF does not have CSS media queries; responsive stacking can be achieved via a `SizeChanged` event handler in code-behind that toggles the grid layout, or via `UniformGrid`/`WrapPanel` if appropriate.
- The hotkey row swap is a straightforward change from `DockPanel.Dock="Left"` (text) / `DockPanel.Dock="Right"` (pills) to the reverse.
