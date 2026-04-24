# WhisperHeim

A local-first, privacy-focused voice toolkit for Windows. Dictate text and record and transcribe calls -- all powered by on-device AI models. No cloud, no subscriptions, no data leaves your machine.

## Features

- **Voice Dictation** -- speak and have text typed into any application (Ctrl+Win)
- **Call Recording & Transcription** -- record system audio with speaker diarization (Ctrl+Win+R)
- **Templates** -- reusable text snippets triggered by voice (Ctrl+Win+Alt)
- **File Transcription** -- drop in existing audio files and get diarized transcripts
- **AI Analysis (optional)** -- summarize and analyze transcripts via a local Ollama model
- **Cloud Sync** -- optionally store your data in a synced folder (Google Drive, OneDrive, Dropbox)

## Prerequisites

- Windows 10/11 (x64)
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

## Build & Run

```bash
git clone https://github.com/heimeshoff/WhisperHeim.git
cd WhisperHeim
dotnet build src/WhisperHeim/WhisperHeim.csproj
dotnet run --project src/WhisperHeim/WhisperHeim.csproj
```

On first launch the app will download the required AI models (~800 MB total). This is a one-time process.

## Publish as Standalone Exe

To create a self-contained executable that runs without .NET installed:

```bash
dotnet publish src/WhisperHeim/WhisperHeim.csproj -c Release -r win-x64 --self-contained -o publish -p:PublishSingleFile=true -v q 2>&1 | tail -5
```

The output will be in `publish/`.

## Project Structure

```
src/WhisperHeim/
  Models/          # Data models and settings
  Services/        # Core services (ASR, hotkeys, recording, diarization, ...)
  Views/           # WPF pages and windows
  Assets/          # Icons, images, branding
```

## Support

If you enjoy WhisperHeim and want to support the project, you can buy me a coffee:

[**Ko-fi -- heimeshoff**](https://ko-fi.com/heimeshoff)

## License

This project is released under [The Unlicense](LICENSE) -- public domain, do whatever you want with it.
