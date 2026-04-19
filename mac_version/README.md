# WhisperHeim for macOS

Hold-to-talk dictation app for macOS. Hold a hotkey, speak, release — transcribed text appears at your cursor.

Uses NVIDIA Parakeet TDT 0.6B v3 (int8) via sherpa-onnx for fast, accurate speech recognition.

## Requirements

- macOS 12+ (Apple Silicon or Intel)
- Python 3.10+
- Microphone access
- Accessibility permissions (for hotkey capture and text insertion)

## Setup

```bash
# Create virtual environment
python3 -m venv .venv
source .venv/bin/activate

# Install dependencies
pip install -r requirements.txt

# Download models (~670 MB, one-time)
python main.py --download

# Run the app
python main.py
```

## Usage

1. Launch the app — it appears in the menu bar
2. Hold **Cmd+Shift** (configurable) and speak
3. Release — transcribed text is typed at your cursor

## Configuration

Settings are stored in `~/Library/Application Support/WhisperHeim/settings.json`.

Default hotkey: **Cmd+Shift** (hold to talk)

To change the hotkey, edit `settings.json`:

```json
{
  "hotkey": {
    "key": "shift",
    "modifiers": ["cmd"]
  }
}
```

Available keys: `shift`, `ctrl`, `alt`, `cmd`, `space`, `tab`, `f1`-`f12`, or any single character.

## Architecture

```
main.py                          Entry point
whisperheim/
  app.py                         Menu bar app (rumps) + orchestration
  services/
    settings_service.py          JSON settings in ~/Library/Application Support/
    model_manager.py             Model download from HuggingFace
    audio_capture.py             16kHz mono capture via sounddevice
    vad_service.py               Silero VAD via sherpa-onnx
    transcription_service.py     Parakeet TDT via sherpa-onnx OfflineRecognizer
    dictation_pipeline.py        Audio -> VAD -> transcribe pipeline
    hotkey_service.py            Global hotkey via pynput
    text_inserter.py             Clipboard + Cmd+V via PyObjC
```

## macOS Permissions

On first run, macOS will ask for:

1. **Microphone access** — required for audio capture
2. **Accessibility** — required for global hotkey capture and text insertion (System Settings > Privacy & Security > Accessibility)

## Models

Models are stored in `~/Library/Application Support/WhisperHeim/models/`:

- **Parakeet TDT 0.6B v3** (int8) — ~670 MB total
  - `encoder.int8.onnx` (~622 MB)
  - `decoder.int8.onnx` (~12 MB)
  - `joiner.int8.onnx` (~6 MB)
  - `tokens.txt` (~9 KB)
- **Silero VAD** — ~2 MB
  - `silero_vad.onnx`
