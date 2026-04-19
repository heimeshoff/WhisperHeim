# Task 001: Mac Dictation Core — Speech-to-Text Pipeline

**Status:** Done
**Size:** Large
**Created:** 2026-04-19
**Milestone:** MVP

## Description

Build the core speech-to-text dictation pipeline for macOS using Python + sherpa-onnx. This is the foundation: hold a hotkey, speak, release, and the transcribed text gets typed into the active application.

## Architecture

- **Tech stack:** Python 3.10+, sherpa-onnx, pynput, sounddevice, rumps, PyObjC
- **Model:** NVIDIA Parakeet TDT 0.6B v3 (int8 quantized, same as Windows WhisperHeim)
- **VAD:** Silero VAD via sherpa-onnx
- **Audio:** 16kHz mono capture via sounddevice
- **Text insertion:** Clipboard + CGEvent Cmd+V via PyObjC
- **Hotkey:** pynput key-down/key-up for hold-to-talk

## Subtasks

- [x] Project scaffolding (directory structure, requirements.txt, entry point)
- [x] Model download script (fetch Parakeet TDT 0.6B + Silero VAD from HuggingFace to ~/Library/Application Support/WhisperHeim/models/)
- [x] Audio capture service (sounddevice, 16kHz mono, start/stop on demand)
- [x] VAD integration (Silero VAD via sherpa-onnx, detect speech segments)
- [x] Transcription service (sherpa-onnx OfflineRecognizer with Parakeet)
- [x] Dictation pipeline (audio capture → VAD → accumulate speech → transcribe on release)
- [x] Global hotkey service (pynput, hold-to-talk with configurable key combo)
- [x] Text insertion (clipboard + Cmd+V via PyObjC CGEvent)
- [x] Menu bar app shell (rumps, with status indicator: idle/recording/transcribing)
- [x] Settings service (JSON config in ~/Library/Application Support/WhisperHeim/settings.json)
- [x] Build/run instructions in README

## Acceptance Criteria

- User holds a hotkey combo (e.g., Cmd+Shift), speaks, releases — transcribed text appears at cursor in active app
- Parakeet model auto-downloads on first run if not present
- Menu bar icon shows recording/transcribing state
- Works on macOS with Apple Silicon and Intel
- Sub-second transcription latency for typical utterances on Apple Silicon
- Requires only `pip install -r requirements.txt` + `python main.py` to run

## Dependencies

- None (first task)

## Technical Notes

- sherpa-onnx Python wheels are prebuilt for macOS ARM64 + x86-64
- User must grant Accessibility permissions for hotkey capture and text insertion
- Reference: JustDictate (github.com/gowtham-ponnana/JustDictate) uses same stack
- Port fuzzy matching and placeholder expansion logic from Windows WhisperHeim

## Work Log

### 2026-04-19 — Implementation Complete

Built the full macOS dictation pipeline, porting architecture patterns from the Windows C#/WPF version. Created 11 files (8 Python modules + main.py + requirements.txt + README.md).

**Services implemented:**
- `settings_service.py` — JSON settings with dataclass schema, auto-loads from ~/Library/Application Support/WhisperHeim/settings.json
- `model_manager.py` — Downloads Parakeet TDT 0.6B v3 int8 + Silero VAD from HuggingFace with progress reporting, atomic downloads, size validation
- `audio_capture.py` — 16kHz mono float32 capture via sounddevice InputStream with callback-based delivery
- `vad_service.py` — Silero VAD via sherpa-onnx VoiceActivityDetector, window-based processing with speech start/end callbacks
- `transcription_service.py` — sherpa-onnx OfflineRecognizer with Parakeet TDT transducer config (16kHz, 80-dim features, greedy_search, 4 threads), thread-safe via lock
- `dictation_pipeline.py` — Hold-to-talk flow: capture -> VAD -> accumulate speech segments -> transcribe on release in background thread
- `hotkey_service.py` — pynput global key listener with configurable combo (default Cmd+Shift), handles key normalization (left/right variants)
- `text_inserter.py` — Clipboard + CGEvent Cmd+V via PyObjC (NSPasteboard + Quartz), saves/restores clipboard
- `app.py` — rumps menu bar app with status indicator (idle/recording/transcribing), orchestrates all services, fallback headless mode

**Key design decisions:**
- Simplified pipeline vs Windows: no partial results during hold-to-talk (user releases to trigger transcription)
- VAD still runs during recording to extract clean speech segments, discarding silence
- Graceful platform fallback: text_inserter and menu bar degrade gracefully on non-macOS
- All sherpa-onnx config matches Windows version exactly (same model URLs, same VAD params)
