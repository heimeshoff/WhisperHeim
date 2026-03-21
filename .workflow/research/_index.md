# Research Index

## ASR Models (2026-03-21)

- **NVIDIA Parakeet TDT 0.6B V3**: Best local model for EN/DE. 6.32% WER, 3333x RTF, ~2-3GB VRAM, no silence hallucinations. 25 European languages.
- **Whisper Large V3 Turbo**: Best multilingual fallback. 0.8B params, 7.75% WER, 99+ languages. Use via faster-whisper for 4x speedup.
- **Distil-Whisper Large V3**: 6x faster than Whisper LV3, within 1% WER. English only.

## Streaming Approaches (2026-03-21)

- **VAD + chunked transcription**: Silero VAD for speech detection, send speech segments to ASR. ~380-520ms latency. Used by WhisperLive, whisper.cpp.
- **Tumbling window (WhisperFlow)**: Accumulate audio, transcribe every cycle, emit when stable. ~275ms latency.
- **LocalAgreement (whisper_streaming)**: Confirm prefix only if N consecutive iterations agree. ~3.3s latency.
- **AlignAtt (SimulStreaming)**: Inspect attention weights to know when to stop decoding. 5x faster than LocalAgreement.

## Speaker Diarization (2026-03-21)

- **sherpa-onnx (NuGet)**: Native .NET, pyannote segmentation 3.0 ONNX, no Python needed. CPU-capable. Best for .NET integration.
- **pyannote-audio**: Gold standard accuracy (~10% DER), Python/PyTorch only.
- **diart**: Real-time streaming diarization, 500ms latency, Python only.
- **WhisperX**: Whisper + pyannote in one pipeline, batch only, Python.

## UI Framework (2026-03-21)

- **WPF + WPF UI (lepoco/wpfui)**: Mica, Fluent controls, tray icon via WPF-UI.Tray. Best fit for Windows 11 tray app.
- **WinUI 3**: No tray support (open issue since 2020). PowerToys uses it but implements tray in raw C++.
