# Protocol

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
