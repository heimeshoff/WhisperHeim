# Task 070: Fix Pill Overlay Visualization

**Status:** Todo
**Size:** Medium
**Milestone:** Bug Fix
**Created:** 2026-03-23

## Problem

The pill-shaped dictation overlay (added in Task 068, commit `c2f5d65`) has two bugs that have been present since it was built:

1. **Bars are invisible** — The pill appears but shows no frequency bars inside it. Audio capture works, but nothing is rendered visually.
2. **Border is always blue** — The pill border stays blue regardless of mic/speech state. Both `Idle` and `Speaking` states use `BlueBorderColor` in `SetMicState()`.

## Root Cause Analysis

### Bars not visible
- `DictationOverlayWindow.xaml`: Window is 150x40, Border has `Padding="8,4"`, `BorderThickness="2"`, `CornerRadius="20"`. The inner Canvas (`BarsCanvas`) may have 0 actual dimensions, or bars are too small/clipped.
- `InitializeBars()` creates 18 rectangles with orange fill. `LayoutBars()` checks `canvasWidth <= 0 || canvasHeight <= 0` and returns early — if Canvas never gets sized, bars never get laid out.
- Verify that `BarsCanvas.ActualWidth` and `ActualHeight` are non-zero after layout.

### Always blue border
- `SetMicState()` lines 320-325: both `Idle` and `Speaking` cases animate to `BlueBorderColor` and set bars to `OrangeBarColor`. There is no visual distinction between the two states.
- XAML default for `PillBorderBrush` is `#FF25abfe` (blue), so even before any state change it appears blue.

## Acceptance Criteria

- [ ] **Grey border + grey/muted bars** when idle (mic active, no speech detected)
- [ ] **Blue border + orange animated bars** when speech is detected — bars respond to voice volume
- [ ] **Grey border + static grey bars** when mic is muted or unavailable (`NoMic` state)
- [ ] **Red fill** for error state (already implemented, verify it works)
- [ ] Bars are actually visible inside the pill and animate smoothly
- [ ] No regression in click-through, positioning, or fade in/out behavior

## Key Files

- `src/WhisperHeim/Views/DictationOverlayWindow.xaml` — pill layout, Canvas, default colors
- `src/WhisperHeim/Views/DictationOverlayWindow.xaml.cs` — bar init, animation tick, state management
- `src/WhisperHeim/Views/OverlayMicState.cs` — state enum
- `src/WhisperHeim/MainWindow.xaml.cs` — amplitude callback wiring (~line 303)

## Implementation Notes

- Fix Canvas sizing: may need explicit `HorizontalAlignment="Stretch"` / `VerticalAlignment="Stretch"` or switch to a layout panel that reports size correctly
- Update `SetMicState(Idle)` to use grey border + grey bars instead of blue/orange
- Update XAML default `PillBorderBrush` to grey so it starts in the correct state
- Consider increasing pill size or bar dimensions if they're too small to see even when properly sized
