# Task: Microphone Selection

**ID:** 014
**Milestone:** M1 - Live Dictation + Core App
**Size:** Small
**Created:** 2026-03-21
**Dependencies:** 003-settings-infrastructure, 006-audio-capture-service

## Objective
Allow the user to select which microphone to use in the settings UI.

## Details
Add a microphone selection dropdown to the Dictation settings page. Enumerate devices via NAudio. Show device names. Default to system default device. Changing the device in settings takes effect on next dictation start (no need for hot-swap). Persist selection in settings.json. Handle the case where a saved device is no longer available (fall back to default, show warning).

## Acceptance Criteria
- [x] All connected mics listed
- [x] Selection persists
- [x] Fallback works for missing devices

## Notes
NAudio device enumeration. No hot-swap needed; takes effect on next dictation start.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 - Implementation complete
- Updated `DictationPage.xaml` with microphone ComboBox in a CardControl, plus a warning TextBlock for missing devices
- Rewrote `DictationPage.xaml.cs` to accept `IAudioCaptureService`, enumerate devices via NAudio, populate the dropdown with "System Default" + all available devices, restore saved selection from settings, and fall back with a warning if the saved device is no longer connected
- Selection persists as device name in `DictationSettings.AudioDevice` (null = system default) via `SettingsService.Save()`
- Added `AudioDeviceResolver` helper to resolve saved device name to NAudio device index for use when starting capture
- Threaded `IAudioCaptureService` through `App.xaml.cs` and `MainWindow.xaml.cs`
- Build succeeds, all 8 existing tests pass
