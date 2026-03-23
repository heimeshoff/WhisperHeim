# Task 068: Pill-Shaped Waveform Overlay at Last Click Position

**Status:** Todo
**Size:** Medium-Large
**Milestone:** UI Polish (Quiet Engine)
**Dependencies:** None

## Description

Replace the current circular pulsing microphone overlay with a horizontal pill-shaped indicator containing animated frequency bars that respond to voice amplitude. The pill appears at the last globally-clicked mouse position instead of a fixed screen location.

## Requirements

### 1. Pill Shape & Frequency Bars
- Horizontal pill shape, approximately 40px tall, ~120-150px wide
- Contains ~15-20 vertical bars inside the pill
- Bars are driven by RMS amplitude with randomized variation per bar to create the visual effect of a frequency response / spectrum analyzer
- When amplitude changes, bars animate to different heights with slight random offsets between them
- Bars should feel responsive and alive while speaking

### 2. Positioning: Last Global Click
- Track the last mouse click position globally (all applications, not just WhisperHeim)
- Requires a low-level global mouse hook (Win32 `SetWindowsHookEx` with `WH_MOUSE_LL`)
- The last-clicked position becomes the **left edge** of the pill; the pill extends to the right
- Remove the existing position settings (BottomCenter, TopLeft, etc.) from `OverlaySettings`

### 3. Visibility
- Same as current: overlay appears when dictation starts, disappears when dictation stops
- Keep fade-in / fade-out animations (0.3s)
- Keep click-through behaviour (`WS_EX_TRANSPARENT`, `WS_EX_NOACTIVATE`)
- Keep always-on-top (`Topmost`)

### 4. Color Scheme (Brand Colors)
- **Dictating (Idle + Speaking):**
  - Pill border: WhisperHeim blue `#FF25abfe`
  - Frequency bars: WhisperHeim orange `#FFff8b00`
  - Bar heights modulated by amplitude (idle = low gentle movement, speaking = active tall bars)
- **No Microphone:**
  - Pill border: Grey
  - Bars: Grey, static (no animation)
- **Error:**
  - Entire pill filled solid red

### 5. Remove Old Overlay
- Remove the circular ellipse + microphone icon glyph from `DictationOverlayWindow.xaml`
- Remove the `ListeningPulse` and `SpeechPulse` storyboard animations
- Remove position enum/settings (`OverlayPosition`, `PositionOnScreen()` method)
- Remove overlay size and position settings from the settings UI if present

## Acceptance Criteria

- [ ] Pill-shaped overlay with ~15-20 vertical frequency bars replaces the old circular mic overlay
- [ ] Bars respond to voice amplitude with randomized per-bar variation (simulated frequency response)
- [ ] Pill appears at the globally last-clicked mouse position (left-anchored, extends right)
- [ ] Global mouse hook tracks clicks across all applications
- [ ] Border is blue (`#25abfe`) and bars are orange (`#ff8b00`) during dictation
- [ ] No-mic state shows grey border and grey static bars
- [ ] Error state fills the pill red
- [ ] Overlay fades in/out with dictation start/stop
- [ ] Overlay is click-through and always-on-top
- [ ] Old position settings removed
- [ ] No visual regressions or focus-stealing

## Technical Notes

- **Files to modify:**
  - `src/WhisperHeim/Views/DictationOverlayWindow.xaml` — replace visual tree
  - `src/WhisperHeim/Views/DictationOverlayWindow.xaml.cs` — new bar rendering, global mouse hook, remove old positioning
  - `src/WhisperHeim/Views/OverlayMicState.cs` — may simplify states
  - `src/WhisperHeim/Models/AppSettings.cs` — remove position/size settings from OverlaySettings
  - `src/WhisperHeim/MainWindow.xaml.cs` — integration point for amplitude updates
- **Bar rendering:** Custom WPF drawing — either a Canvas with Rectangle children updated per frame, or a custom `OnRender` override for performance
- **Global mouse hook:** `SetWindowsHookEx(WH_MOUSE_LL, ...)` via P/Invoke; store last `WM_LBUTTONDOWN` screen coordinates
- **Amplitude → bars:** On each `UpdateAmplitude(rms)` call, generate per-bar heights as `baseHeight * rms * (1 + randomOffset)` where randomOffset varies per bar per update
