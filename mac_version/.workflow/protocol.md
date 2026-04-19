## 2026-04-19 -- Task Completed: 001 - Mac Dictation Core

**Type:** Task Completion
**Task:** 001 - Mac Dictation Core — Speech-to-Text Pipeline
**Summary:** Built the complete macOS dictation pipeline (11 new files) porting the Windows WhisperHeim architecture to Python. Implemented project scaffolding, model download manager, audio capture, VAD, transcription, dictation pipeline, global hotkey, text insertion, menu bar app, settings service, and README.
**Files changed:** 12 files

---

## 2026-04-19 -- Task Started: 001 - Mac Dictation Core

**Type:** Task Start
**Task:** 001 - Mac Dictation Core — Speech-to-Text Pipeline
**Milestone:** MVP

---

## 2026-04-19 -- Idea Captured: WhisperHeim Mac Port

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/001-mac-dictation-core.md, tasks/todo/002-mac-template-system.md
**Summary:** Stripped-down macOS port of WhisperHeim with two features: (1) hold-to-talk speech-to-text using Parakeet TDT 0.6B via sherpa-onnx, and (2) voice-triggered template expansion with fuzzy matching. Built as a Python menu bar app using rumps, pynput, sounddevice, and PyObjC. Two tasks created: 001 for the core dictation pipeline, 002 for the template system.

---
