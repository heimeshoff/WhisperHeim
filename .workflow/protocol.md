# Protocol

---

## 2026-03-21 21:30 -- Task Completed: 034 - Audio export (MP3/OGG/WAV)

**Type:** Task Completion
**Task:** 034 - Audio export (MP3/OGG/WAV)
**Summary:** Created AudioExportService with WAV, MP3 (resampled to 44.1kHz via MediaFoundationEncoder), and OGG/Opus (resampled to 48kHz via Concentus) export. Added "Save as..." button to TextToSpeechPage with SaveFileDialog and format selection.
**Files changed:** 4 files

---

## 2026-03-21 21:20 -- Task Started: 034 - Audio export (MP3/OGG)

**Type:** Task Start
**Task:** 034 - Audio export (MP3/OGG/WAV)
**Milestone:** M4 - Text-to-Speech

---

## 2026-03-21 21:15 -- Task Completed: 035 - TTS settings + hotkey configuration

**Type:** Task Completion
**Task:** 035 - TTS settings + hotkey configuration
**Summary:** Added TtsSettings model with DefaultVoiceId, ReadAloudHotkey, PlaybackDeviceId persisted via SettingsService. ReadAloudHotkeyService now reads hotkey config from settings with live re-registration. SpeakAsync accepts playback device parameter.
**Files changed:** 6 files

---

## 2026-03-21 21:12 -- Task Completed: 033 - TTS UI page

**Type:** Task Completion
**Task:** 033 - TTS UI page
**Summary:** Created TextToSpeechPage with multi-line text input, voice selector (built-in + custom), Play/Stop with CancellationTokenSource, indeterminate progress bar, and voice preview button. Wired into MainWindow sidebar navigation.
**Files changed:** 5 files

---

## 2026-03-21 21:05 -- Batch Started: [033, 035]

**Type:** Batch Start
**Tasks:** 033 - TTS UI page, 035 - TTS settings + hotkey configuration
**Mode:** Parallel (batch of 2)

---

## 2026-03-21 21:00 -- Task Completed: 032 - Read selected text via global hotkey

**Type:** Task Completion
**Task:** 032 - Read selected text via global hotkey
**Summary:** Implemented SelectedTextService with cascading capture (UI Automation TextPattern first, then SendInput Ctrl+C with clipboard backup/restore) and ReadAloudHotkeyService (Ctrl+Shift+R default) that speaks captured text via ITextToSpeechService.
**Files changed:** 5 files

---

## 2026-03-21 20:58 -- Task Completed: 031 - Voice cloning from system audio loopback

**Type:** Task Completion
**Task:** 031 - Voice cloning from system audio loopback
**Summary:** Created HighQualityLoopbackService capturing system audio at native 48kHz via WasapiLoopbackCapture. Built VoiceLoopbackCapturePage UI with device selection, level meter, duration display, voice naming, and save to voices directory.
**Files changed:** 6 files

---

## 2026-03-21 20:55 -- Task Completed: 030 - Voice cloning from microphone recording

**Type:** Task Completion
**Task:** 030 - Voice cloning from microphone recording
**Summary:** Implemented HighQualityRecorderService recording mic at 44.1kHz and VoiceCloningPage UI with level meter, duration tracking, 5s minimum indicator, device selection, voice naming, and background noise warning.
**Files changed:** 7 files

---

## 2026-03-21 20:50 -- Batch Started: [030, 031, 032]

**Type:** Batch Start
**Tasks:** 030 - Voice cloning from mic, 031 - Voice cloning from loopback, 032 - Read selected text via hotkey
**Mode:** Parallel (batch of 3)

---

## 2026-03-21 20:45 -- Task Completed: 029 - Pocket TTS engine service + model download

**Type:** Task Completion
**Task:** 029 - Pocket TTS engine service + model download + built-in voice playback
**Summary:** Implemented ITextToSpeechService with Pocket TTS via sherpa-onnx C# bindings. Supports GenerateAudioAsync, streaming generation with callback, and SpeakAsync with NAudio WaveOutEvent playback at 24kHz. Added PocketTtsInt8 model (~200MB, 9 files) to ModelManagerService for auto-download from HuggingFace. Build succeeds.
**Files changed:** 5 files

---

## 2026-03-21 19:15 -- Task Started: 029 - Pocket TTS engine service + model download

**Type:** Task Start
**Task:** 029 - Pocket TTS engine service + model download + built-in voice playback
**Milestone:** M4 - Text-to-Speech

---

## 2026-03-21 18:50 -- Idea Captured: Kyutai Pocket TTS Integration

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/023-tts-pocket-tts.md (parent) + subtasks 029–035
**Summary:** Full TTS milestone using Kyutai Pocket TTS — voice cloning from mic/loopback, read-selected-text hotkey (UI Automation + Ctrl+C fallback), TTS UI page, MP3/OGG export, and settings. Researched feasibility of all components: Pocket TTS runs CPU-only via sherpa-onnx (already a dependency), text selection capture is proven pattern, loopback capture infrastructure exists. English-only, 7 tasks total.

---

## 2026-03-21 -- Task Completed: 028 - Post-recording transcription pipeline with progress UI

**Type:** Task Completion
**Task:** 028 - Post-recording transcription pipeline with progress UI
**Summary:** Created TranscriptionProgressDialog with dual progress bars, stage description, and cancel button. Wired into MainWindow to auto-trigger pipeline when recording stops, with navigation to TranscriptsPage on success.
**Files changed:** 4 files

---

## 2026-03-21 -- Task Started: 028 - Post-recording transcription pipeline with progress UI

**Type:** Task Start
**Task:** 028 - Post-recording transcription pipeline with progress UI
**Milestone:** M2 - Audio Capture + Call Transcription

---

## 2026-03-21 -- Task Completed: 027 - Tray context menu for start/stop call recording

**Type:** Task Completion
**Task:** 027 - Tray context menu for start/stop call recording
**Summary:** Added "Start Call Recording" tray menu item with Record24 icon, Ctrl+Win+R hotkey, and live recording state feedback (orange tray icon, duration in menu text and tooltip). Added DurationUpdated to ICallRecordingService interface.
**Files changed:** 4 files

---

## 2026-03-21 -- Task Started: 027 - Tray context menu for start/stop call recording

**Type:** Task Start
**Task:** 027 - Tray context menu for start/stop call recording
**Milestone:** M2 - Audio Capture + Call Transcription

---

## 2026-03-21 -- Task Completed: 026 - Wire call recording services in app startup

**Type:** Task Completion
**Task:** 026 - Wire call recording services in app startup
**Summary:** Wired CallRecordingService, CallTranscriptionPipeline, CallRecordingHotkeyService, SpeakerDiarizationService, and TranscriptStorageService in App.xaml.cs and passed them to MainWindow constructor as fields.
**Files changed:** 3 files

---

## 2026-03-21 -- Task Started: 026 - Wire call recording services in app startup

**Type:** Task Start
**Task:** 026 - Wire call recording services in app startup
**Milestone:** M2 - Audio Capture + Call Transcription

---

## 2026-03-21 -- Planning: Call Recording UI Integration

**Type:** Planning
**Summary:** Planned 3 tasks to wire up the existing call recording backend to the UI — service registration, tray context menu with Ctrl+Win+R hotkey, and post-recording transcription progress dialog with auto-navigation.
**Milestones created/updated:** M2 (added tasks 026-028)
**Tasks created:** 026-wire-call-recording-services, 027-tray-menu-call-recording, 028-post-recording-transcription-ui
**Tasks moved to backlog:** none
**Ideas incorporated:** none

---

## 2026-03-21 -- Task Completed: 025 - Overlay Microphone State Visualization

**Type:** Task Completion
**Task:** 025 - Overlay Microphone State Visualization
**Summary:** Implemented dynamic overlay mic states (green idle, green+RMS-driven ring scaling while speaking, grey for no mic, red for errors). Added OverlayMicState enum, replaced hardcoded red with animated color brushes, wired real-time audio amplitude through orchestrator to drive smooth ring scaling.
**Files changed:** 5 files

---

## 2026-03-21 -- Task Started: 025 - Overlay Microphone State Visualization

**Type:** Task Start
**Task:** 025 - Overlay Microphone State Visualization
**Milestone:** --

---

## 2026-03-21 -- Idea Captured: Overlay Mic State Visualization

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/025-overlay-mic-state-visualization.md
**Summary:** Dynamic mic icon colors (green=idle/speaking, grey=no mic, red=error) with amplitude-driven ring scaling animation during speech. Overlay only.

---

## 2026-03-21 -- Task Completed: 024 - Windows Auto-Launch

**Type:** Task Completion
**Task:** 024 - Windows Auto-Launch
**Summary:** StartupService manages HKCU Run registry, --minimized flag for tray-only auto-start, path refresh on each launch.
**Files changed:** 5 files

---

## 2026-03-21 -- Task Completed: 014 - Microphone Selection

**Type:** Task Completion
**Task:** 014 - Microphone Selection
**Summary:** Dropdown on Dictation page with NAudio device enumeration, persisted selection, fallback for missing devices.
**Files changed:** 5 files

---

## 2026-03-21 -- Task Completed: 005 - Model Manager

**Type:** Task Completion
**Task:** 005 - Model Manager
**Summary:** Auto-downloads Parakeet TDT 0.6B int8 (~661MB) and Silero VAD (~2MB) on first run with progress dialog and cancellation.
**Files changed:** 9 files

---

## 2026-03-21 -- Task Completed: 007 - Silero VAD Integration

**Type:** Task Completion
**Task:** 007 - Silero VAD Integration
**Summary:** ONNX Runtime-based Silero VAD with state machine, configurable thresholds, pre-speech padding, SpeechStarted/SpeechEnded events.
**Files changed:** 4 files

---

## 2026-03-21 -- Task Completed: 004 - Global Hotkey

**Type:** Task Completion
**Task:** 004 - Global Hotkey
**Summary:** Win32 RegisterHotKey/UnregisterHotKey with configurable Ctrl+LWin hotkey, event system, and conflict handling.
**Files changed:** 4 files

---

## 2026-03-21 -- Task Completed: 003 - Settings Infrastructure

**Type:** Task Completion
**Task:** 003 - Settings Infrastructure
**Summary:** JSON settings with AppSettings model, SettingsService for %APPDATA% persistence, and 4 navigable settings pages in MainWindow.
**Files changed:** 13 files

---

## 2026-03-21 -- Batch Started: [003, 004, 007]

**Type:** Batch Start
**Tasks:** 003 - Settings Infrastructure, 004 - Global Hotkey, 007 - Silero VAD Integration
**Mode:** Parallel (batch of 3)

---

## 2026-03-21 -- Task Completed: 010 - Input Simulation

**Type:** Task Completion
**Task:** 010 - Input Simulation
**Summary:** Win32 SendInput P/Invoke with KEYEVENTF_UNICODE, backspace correction, configurable delay, cancellation support.
**Files changed:** 4 files

---

## 2026-03-21 -- Task Completed: 006 - Audio Capture Service

**Type:** Task Completion
**Task:** 006 - Audio Capture Service
**Summary:** NAudio WaveInEvent capture at 16kHz/mono, float32 conversion, thread-safe ring buffer, device enumeration. 8 passing unit tests.
**Files changed:** 8 files

---

## 2026-03-21 -- Task Completed: 002 - Tray Icon and Window

**Type:** Task Completion
**Task:** 002 - Tray Icon and Window
**Summary:** FluentWindow with Mica backdrop, tray icon with Segoe Fluent microphone glyph, show/hide toggle, right-click context menu.
**Files changed:** 5 files

---

## 2026-03-21 -- Batch Started: [002, 006, 010]

**Type:** Batch Start
**Tasks:** 002 - Tray Icon and Window, 006 - Audio Capture Service, 010 - Input Simulation
**Mode:** Parallel (batch of 3)

---

## 2026-03-21 -- Task Completed: 001 - Project Scaffolding

**Type:** Task Completion
**Task:** 001 - Project Scaffolding
**Summary:** Created .NET 9 WPF solution with all core NuGet packages, x64-only config, and ShutdownMode=OnExplicitShutdown. Builds with 0 warnings.
**Files changed:** 8 files

---

## 2026-03-21 -- Task Started: 001 - Project Scaffolding

**Type:** Task Start
**Task:** 001 - Project Scaffolding
**Milestone:** M1 - Live Dictation + Core App

---

## 2026-03-21 -- Planning: Full roadmap and task breakdown for all milestones

**Type:** Planning
**Summary:** Created 4-milestone roadmap with 24 tasks. M1 (Live Dictation + Core App) has 15 tasks covering project setup through end-to-end dictation with overlay and templates. M2 (Call Transcription) has 5 tasks for WASAPI loopback, diarization, and transcript export. M3 (Voice Messages) has 3 tasks including backlogged Telegram bot. M4 (TTS) is a single placeholder task in backlog.
**Milestones created:** M1, M2, M3, M4
**Tasks created:** 001 through 024
**Tasks moved to backlog:** 022-telegram-bot, 023-tts-integration
**Ideas incorporated:** None (no ideas existed)

---

## 2026-03-21 -- Brainstorm: Initial product vision for WhisperHeim

**Type:** Brainstorm
**Summary:** Defined WhisperHeim as a local-first, Windows 11 tray app unifying all voice workflows: live streaming dictation, call transcription with speaker diarization, voice message transcription, and text-to-speech. Chose C#/WPF with WPF UI for the native shell, Parakeet TDT 0.6B for ASR, sherpa-onnx for diarization, and Silero VAD for streaming.
**Vision updated:** Yes
**Key decisions:**
- Complete restart from VocalFold -- new architecture, no code reuse
- C# across the board (systems-level complexity favors C# over F#)
- WPF + WPF UI (not WinUI 3) for tray app with PowerToys aesthetics
- Parakeet TDT 0.6B over Whisper (faster, no hallucinations, EN/DE sufficient)
- sherpa-onnx for both ASR and diarization (native .NET, no Python sidecar)
- WASAPI loopback for system audio capture (call transcription = Milestone 2)
- Text-to-speech deferred to Milestone 4, details TBD
- No voice commands -- templates only, triggered by hotkey + voice
- Telegram bot integration as stretch goal for voice message transcription

---
