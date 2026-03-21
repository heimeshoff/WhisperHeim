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

**Tasks:** 015, 016, 017, 018, 019, 026, 027, 028

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

### M4: Text-to-Speech
**Status:** Not Started
**Target:** AI voice generation (details TBD)

**Goals:**
- Feed text, hear it read aloud with selectable voices
- Model and approach to be decided

**Tasks:** 023 (backlog)
