# WhisperHeim -- Roadmap

---

### M1: Live Dictation + Core App
**Status:** Not Started
**Target:** Core experience -- dictate anywhere on Windows 11 with a hotkey

**Goals:**
- Tray app with PowerToys-inspired Fluent UI
- Live streaming dictation with <2s latency via Parakeet TDT + Silero VAD
- Text inserted at cursor in any application via SendInput
- Text templates triggered by hotkey + voice
- Auto model download on first run
- On-screen dictation indicator (animated, non-intrusive)
- Optional Windows startup auto-launch

**Tasks:** 001, 002, 003, 004, 005, 006, 007, 008, 009, 010, 011, 012, 013, 014, 024

---

### M2: Audio Capture + Call Transcription
**Status:** Not Started
**Target:** Record and transcribe calls with speaker separation

**Goals:**
- WASAPI loopback captures system audio (Zoom, Meet, etc.)
- Dual capture: mic + system audio simultaneously
- Speaker diarization via sherpa-onnx (pyannote ONNX)
- Timestamped, speaker-attributed transcripts
- Transcript viewer with export (Markdown, JSON, TXT)

**Tasks:** 015, 016, 017, 018, 019, 026, 027, 028, 036, 037, 038, 075, 076, 077, 078, 079, 097, 098, 099

---

### M3: Voice Message Transcription
**Status:** Not Started
**Target:** Transcribe audio files from WhatsApp, Telegram, etc.

**Goals:**
- Drag-and-drop OGG/MP3/M4A files for transcription
- Batch processing support
- Telegram bot integration (stretch goal, backlog)

**Tasks:** 020, 021, 022 (backlog)

---

### M4: Text-to-Speech (Kyutai Pocket TTS)
**Status:** Removed (Task 103 — feature deleted)
**Target:** Local AI voice generation with voice cloning and read-aloud hotkey

**Goals:**
- Kyutai Pocket TTS integration via sherpa-onnx (CPU-only, 100M params, English)
- Voice cloning from mic recording or system audio loopback (≥5s reference)
- Global hotkey to read selected text aloud from any application (UI Automation + Ctrl+C fallback)
- Paste-and-read UI with voice selector and playback controls
- Export generated speech as MP3, OGG, or WAV
- Custom voice management (create, name, preview, delete)

**Tasks:** 023, 029, 030, 031, 032, 033, 034, 035

---

### M5: Public Release (GitHub Distribution)
**Status:** Not Started
**Target:** Ship WhisperHeim as a downloadable installer on GitHub Releases with auto-update

**Goals:**
- Velopack-based installer + auto-update pipeline
- First-launch model download UX (640 MB Parakeet) with progress + pause/resume
- Small models (Silero VAD, Pyannote Seg 3.0) bundled with the app
- FFmpeg detected at startup; user prompted to install via `winget` when missing
- Tagged-release GitHub Actions workflow producing `Setup.exe` + delta packages
- Public README + Release page with SmartScreen click-through guidance
- Uninstall preserves user data (`%AppData%\WhisperHeim` and configurable `DataPath`)
- Code-signing slot stubbed for post-UG activation

**Tasks:** 107, 108, 109, 110, 111, 112, 113, 114, 115

**Research:** `.workflow/research/installer-and-github-distribution.md` (2026-05-12), `.workflow/research/auto-update-and-distribution.md` (2026-03-27)
