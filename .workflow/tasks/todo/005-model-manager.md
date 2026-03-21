# Task: Model Manager

**ID:** 005
**Milestone:** M1 - Live Dictation + Core App
**Size:** Medium
**Created:** 2026-03-21
**Dependencies:** 003-settings-infrastructure

## Objective
Auto-download Parakeet TDT 0.6B and Silero VAD models on first run with progress indication.

## Details
Models stored in %APPDATA%/WhisperHeim/models/. Check if models exist on startup. If missing, show a download dialog with progress bar. Download Parakeet TDT 0.6B ONNX model from HuggingFace (or sherpa-onnx model zoo). Download Silero VAD ONNX model (~1MB). Support cancellation. Show model status on the About settings page (model name, size, path). Use HttpClient with progress reporting.

## Acceptance Criteria
- [ ] First run downloads models with visible progress
- [ ] Subsequent runs skip download
- [ ] Models are verified (file size/hash)
- [ ] Cancellation works

## Notes
Parakeet TDT 0.6B from HuggingFace or sherpa-onnx model zoo. Silero VAD is ~1MB.

## Work Log
<!-- Appended by /work during execution -->
