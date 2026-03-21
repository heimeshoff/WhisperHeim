# Protocol

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
