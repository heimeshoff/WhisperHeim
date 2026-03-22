# Task: Tray Icon Green When Recording

**ID:** 048
**Milestone:** --
**Size:** Small
**Created:** 2026-03-22
**Dependencies:** --

## Objective
Change the tray icon recording state from red to green, matching the on-screen overlay's green indicator.

## Details
When recording, the tray icon microphone currently turns red. It should instead use the same green color as the visual overlay icon. Find the green value used in the overlay and apply it to the tray icon's recording state.

## Acceptance Criteria
- [x] Tray icon turns green (not red) when recording
- [x] Green matches the overlay's recording indicator color
- [x] Non-recording state remains unchanged

## Work Log
<!-- Appended by /work during execution -->
- 2026-03-22: Changed tray icon recording color from `Brushes.Red` to `new SolidColorBrush(Color.FromRgb(0x44, 0xCC, 0x44))` in MainWindow.xaml.cs, matching the overlay's GreenColor. Idle and call-recording icon colors unchanged. Build verified.
