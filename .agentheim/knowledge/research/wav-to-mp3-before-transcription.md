# Research: WAV-to-MP3 Conversion Before Transcription & Diarization

**Date:** 2026-03-31
**Status:** Complete
**Relevance:** Milestone 2 (Call Transcription) -- storage optimization for recorded calls

## Summary

Converting WAV recordings to MP3 before transcription and diarization is fully feasible with negligible quality impact. Modern ASR models (Parakeet TDT, Whisper, sherpa-onnx) are robust to MP3 compression at 64+ kbps. Speaker diarization is slightly more sensitive due to reliance on fine-grained spectral features for speaker embeddings, but remains reliable at 128 kbps mono.

The current pipeline records dual streams (mic + system) as 16kHz mono 32-bit float WAV (~64 KB/s per stream). Converting to 128 kbps mono MP3 yields a 4x size reduction -- a 1-hour dual-stream call drops from ~460 MB to ~115 MB. Implementation is straightforward via NAudio.Lame NuGet, which integrates directly with the existing NAudio stack.

## Key Findings

### ASR Accuracy Is Unaffected at 64+ kbps

Whisper was tested down to 32 kbps / 12 kHz mono with no measurable accuracy degradation. NVIDIA Parakeet natively supports MP3 input and decodes to PCM internally -- the model sees identical waveforms regardless of container format. sherpa-onnx expects PCM at the API level, but the .NET pipeline already decodes to PCM before feeding the model, making source format irrelevant.

AssemblyAI recommends 128 kbps+ and warns that poor format choices can degrade accuracy by 15-30%, but this figure covers extreme cases (very low bitrate, wrong sample rate). Google Cloud Speech-to-Text advises keeping lossless originals for legal/medical use cases.

- Source: [Max Rohde: Optimise Whisper API Audio Format](https://dev.to/mxro/optimise-openai-whisper-api-audio-format-sampling-rate-and-quality-29fj)
- Source: [AssemblyAI: Best Audio Formats for STT](https://www.assemblyai.com/blog/best-audio-file-formats-for-speech-to-text)
- Source: [Google Cloud: Audio Encoding for STT](https://cloud.google.com/speech-to-text/docs/encoding)

### Diarization Needs 128 kbps to Be Safe

Speaker diarization relies on speaker embeddings (high-dimensional vectors capturing pitch patterns, formant frequencies, speaking style). MP3 removes subtle acoustic features in the 10-16 kHz range that contribute to speaker discrimination. Research on speaker embedding robustness (Ferro Filho et al., Interspeech 2025) shows compression artifacts reduce discriminative power, especially for models trained only on clean data. Modern models like ECAPA2 include compression augmentation during training, improving robustness.

No published study specifically benchmarks pyannote or sherpa-onnx diarization on MP3 vs WAV. At 128 kbps+, spectral distortion is minimal and unlikely to cause clustering errors in typical meeting scenarios. At 64 kbps, degradation may appear with very similar-sounding speakers.

- Source: [Ferro Filho et al.: Evaluating Deep Speaker Embedding Robustness (Interspeech 2025)](https://www.isca-archive.org/interspeech_2025/ferrofilho25_interspeech.pdf)

### Recommended Bitrate: 128 kbps Mono

| Use case | Minimum | Recommended |
|---|---|---|
| ASR only | 32 kbps | 64 kbps |
| ASR + diarization | 64 kbps | **128 kbps** |
| Archival | 128 kbps | 128 kbps |

No benefit to going above 128 kbps for speech-only content.

### Storage Savings

| Format | Size/min | Size/hour | vs 32-bit float WAV |
|---|---|---|---|
| WAV 16kHz 32-bit float (current) | 3.84 MB | 230 MB | 1x |
| WAV 16kHz 16-bit PCM | 1.92 MB | 115 MB | 2x |
| MP3 128 kbps | 0.96 MB | 57.6 MB | **4x** |
| MP3 64 kbps | 0.48 MB | 28.8 MB | **8x** |

Dual-stream (mic + system) 1-hour call: current ~460 MB → ~115 MB at 128 kbps MP3.

### .NET Implementation: NAudio.Lame

Best fit for the existing stack. Already uses NAudio for all audio I/O.

```csharp
// NuGet: NAudio.Lame (2.1.0)
using NAudio.Wave;
using NAudio.Lame;

public static async Task ConvertWavToMp3Async(string wavPath, string mp3Path, int bitrate = 128)
{
    using var reader = new AudioFileReader(wavPath);
    using var writer = new LameMP3FileWriter(mp3Path, reader.WaveFormat, bitrate);
    await reader.CopyToAsync(writer);
}
```

- Pure .NET, no external process, in-memory support
- Bundles native libmp3lame DLLs (Windows)
- Cross-platform variant available: `NAudio.Lame.CrossPlatform`
- [NuGet](https://www.nuget.org/packages/NAudio.Lame) | [GitHub](https://github.com/Corey-M/NAudio.Lame)

Alternative: FFMpegCore if FFmpeg is already deployed, but adds an external dependency.

## Implications for This Project

1. **Convert after recording, before pipeline**: When `CallRecordingService` stops, convert both `mic.wav` and `system.wav` to MP3. Then feed MP3 paths (or decoded PCM) to the transcription pipeline.
2. **No pipeline changes needed for ASR**: `AudioFileDecoder` already supports MP3 via `MediaFoundationReader`. The existing `LoadWavSamples()` / `LoadWavSegment()` functions use `AudioFileReader` which handles MP3 natively.
3. **Diarization path needs no change**: `DiarizeFromFileAsync()` loads via `LoadWavSamples()` which goes through `AudioFileReader` -- supports MP3.
4. **Quick win**: Could also convert to 16-bit PCM WAV first (2x savings, zero quality risk) as an intermediate step before full MP3 conversion.
5. **Keep originals optionally**: For a transition period, keep WAV files until MP3 diarization quality is validated in practice.

## Open Questions

- Should the conversion happen synchronously (blocking before transcription) or asynchronously (convert in background, start transcription when ready)?
- Should original WAV files be deleted automatically or kept for a configurable retention period?
- Would FLAC (lossless, ~2x compression) be a better middle ground if diarization quality concerns arise?
- Should drag-and-drop file transcription (Milestone 3) also benefit from pre-conversion, or only call recordings?

## Sources

- [Max Rohde: Optimise OpenAI Whisper API Audio Format](https://dev.to/mxro/optimise-openai-whisper-api-audio-format-sampling-rate-and-quality-29fj)
- [AssemblyAI: Best Audio File Formats for Speech-to-Text](https://www.assemblyai.com/blog/best-audio-file-formats-for-speech-to-text)
- [Google Cloud: Audio Encoding for Speech-to-Text](https://cloud.google.com/speech-to-text/docs/encoding)
- [Ferro Filho et al.: Evaluating Deep Speaker Embedding Robustness (Interspeech 2025)](https://www.isca-archive.org/interspeech_2025/ferrofilho25_interspeech.pdf)
- [NVIDIA Parakeet TDT v2 Model Card](https://huggingface.co/nvidia/parakeet-tdt-0.6b-v2)
- [sherpa-onnx GitHub](https://github.com/k2-fsa/sherpa-onnx)
- [NAudio.Lame NuGet](https://www.nuget.org/packages/NAudio.Lame)
- [NAudio.Lame GitHub](https://github.com/Corey-M/NAudio.Lame)
