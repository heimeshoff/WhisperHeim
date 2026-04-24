# Task: Remove Text-to-Speech Feature

**ID:** 103
**Milestone:** --
**Size:** Medium
**Created:** 2026-04-24
**Status:** Ready
**Dependencies:** None (coordinate with 102 — see §Coordination)

## Objective

Remove the Text-to-Speech feature from WhisperHeim in its entirety — UI page, service code, model definitions, voice cloning, read-aloud hotkey, settings, and on-disk artifacts on user machines. WhisperHeim is a dictation/transcription tool; TTS is not part of its scope.

## Background

TTS was added across tasks 023 / 029 / 033 / 035 / 040 / 042 / 047 / 062 — a full page with Kyutai Pocket TTS (sherpa-onnx), WAV/MP3/OGG export, mic + system-audio voice cloning with custom-voice storage under `%APPDATA%\WhisperHeim\voices`, and a global read-aloud hotkey that pipes highlighted text into the page. The feature is self-contained: dictation, diarization, VAD, Ollama analysis, and transcript management do not depend on it.

All shared infrastructure (NAudio, sherpa-onnx, ONNX runtime, Concentus, `HighQualityRecorderService`, `HighQualityLoopbackService`, `AudioFileDecoder`) is also used by dictation and recording flows and **must stay**.

## Scope of Deletion

### 1. UI page
- `src/WhisperHeim/Views/Pages/TextToSpeechPage.xaml`
- `src/WhisperHeim/Views/Pages/TextToSpeechPage.xaml.cs`

### 2. Service layer
- `src/WhisperHeim/Services/TextToSpeech/ITextToSpeechService.cs`
- `src/WhisperHeim/Services/TextToSpeech/TextToSpeechService.cs`
- `src/WhisperHeim/Services/TextToSpeech/AudioExportService.cs`
- Delete the `Services/TextToSpeech/` directory itself if empty after removal

### 3. Read-aloud hotkey pipeline
- Delete `ReadAloudHotkeyService` and any selected-text capture pipeline that exists solely to feed it
- Remove the hotkey registration + navigation handler from `MainWindow.xaml.cs` (lines ~318–340, ~324–337)
- Remove `TtsSettings.ReadAloudHotkey` (covered in §5)

### 4. Navigation integration
- `MainWindow.xaml`: remove the `TextToSpeech` `ListBoxItem` (lines ~154–157) and `NavLabelTextToSpeech`
- `MainWindow.xaml.cs`: remove the `TextToSpeech` case from `NavigateTo()` switch (~797–802), the page factory `CreateTextToSpeechPage()`, the nav-label visibility line (~849), and any `_pageCache` entry
- Resource strings for `NavLabelTextToSpeech` and any TTS-only localized strings

### 5. Settings & config
- `AppSettings.cs`: delete the `TtsSettings` class (lines ~190–210) and its property on `AppSettings`
- `BootstrapConfig.cs`: delete `TtsPlaybackDeviceId` (line ~31–33) and its sync hookup in `SettingsService.SyncFromBootstrap()` / `SyncToBootstrap()`
- `App.xaml.cs`: remove `TextToSpeechService` instantiation (~197–198), `Initialize(_dataPathService)` call (~139–140), warm-up scheduling (~257+), and DI registration

### 6. Model definitions
- `Services/Models/ModelManagerService.cs`: remove `PocketTtsFp32` definition (~103–192), `PocketTtsInt8` definition, `ActivePocketTtsModel` property (~260–268), and their entries in `KnownModels` (~197)

### 7. On-disk artifacts — one-time cleanup on first run after upgrade
Add cleanup logic that runs once on first launch of the post-TTS-removal build:
- **Models:** delete Pocket TTS model files from the models directory (`lm_flow.onnx`, `lm_main.onnx`, `encoder.onnx`, `decoder.onnx`, `text_conditioner.onnx`, `vocab.json`, `token_scores.json`, both FP32 and int8 variants, plus `test_wavs/`)
- **Custom voices:** delete `%APPDATA%\WhisperHeim\voices` (or `{DataPath}/voices`) and all contents
- **Settings:** actively strip the `"tts"` key from `settings.json` during settings-load migration so the field disappears from users' files
- Gate the cleanup on a bootstrap flag (`tts_cleanup_done: true`) so it only runs once
- Log what was removed at Info level for traceability

### 8. Project references
- Verify nothing else in `.csproj` needs removing. Per the survey, all TTS-adjacent NuGet packages (NAudio, sherpa-onnx, Concentus, ONNX Runtime) are shared with STT/diarization/VAD and must stay.

## Coordination

- **Task 102 (Hot-Reload Settings from Disk)** — in `todo/`, not yet started — currently lists `TextToSpeechPage` as a `SettingsChanged` subscriber (see task 102 §5). If 103 lands first, drop that bullet from 102's scope. If 102 lands first, 103 must also remove the subscription. Land 103 first to simplify.

## Acceptance Criteria

- [ ] `Views/Pages/TextToSpeechPage.*` deleted; no XAML or code-behind references remain
- [ ] `Services/TextToSpeech/` directory fully removed
- [ ] `ReadAloudHotkeyService` and its selected-text capture pipeline deleted; no hotkey registration for read-aloud remains
- [ ] Navigation sidebar no longer shows a TTS pill; `NavigateTo("TextToSpeech")` is unreachable and the case is gone
- [ ] `TtsSettings` class and property deleted from `AppSettings.cs`; `TtsPlaybackDeviceId` removed from `BootstrapConfig.cs` and sync paths
- [ ] Pocket TTS model definitions and `ActivePocketTtsModel` removed from `ModelManagerService.cs`
- [ ] `App.xaml.cs` no longer instantiates, initializes, or warms up `TextToSpeechService`
- [ ] Solution builds clean with no unresolved symbols or dangling `using WhisperHeim.Services.TextToSpeech;` imports
- [ ] Existing TTS tests (none found) — n/a
- [ ] First-run cleanup: Pocket TTS model files are deleted from the models directory on first launch after upgrade
- [ ] First-run cleanup: `%APPDATA%\WhisperHeim\voices` (or `{DataPath}/voices`) is deleted with contents
- [ ] First-run cleanup: `"tts"` key is stripped from `settings.json` during load migration and not re-written on save
- [ ] First-run cleanup guarded by a bootstrap flag so it runs exactly once
- [ ] Smoke test: app launches, sidebar is intact, dictation works, file transcription works, streams/video works, recording works, Ollama analysis works, settings load & save correctly
- [ ] Smoke test: existing user with `tts` key in settings.json and downloaded Pocket TTS model upgrades cleanly — model files and voices folder gone, settings file no longer contains `tts` block, no errors in logs
- [ ] Workflow docs under `.workflow/tasks/done/` (023, 029, 033, 035, 040, 042, 047, 062) left in place as historical record

## Open Questions

None — scoped in capture conversation 2026-04-24.

## References

- `src/WhisperHeim/Views/Pages/TextToSpeechPage.xaml` + `.cs`
- `src/WhisperHeim/Services/TextToSpeech/`
- `src/WhisperHeim/Services/Models/ModelManagerService.cs`
- `src/WhisperHeim/MainWindow.xaml` + `.cs`
- `src/WhisperHeim/App.xaml.cs`
- `src/WhisperHeim/Models/AppSettings.cs`, `BootstrapConfig.cs`
- Historical: tasks 023, 029, 033, 035, 040, 042, 047, 062
