using System.Diagnostics;
using System.IO;
using Concentus;
using Concentus.Oggfile;
using NAudio.Wave;

namespace WhisperHeim.Services.TextToSpeech;

/// <summary>
/// Exports float32 audio samples to WAV, MP3, or OGG/Opus files.
/// </summary>
internal sealed class AudioExportService
{
    /// <summary>
    /// Exports audio samples to a WAV file (16-bit PCM).
    /// </summary>
    public async Task ExportToWavAsync(float[] samples, int sampleRate, string filePath)
    {
        await Task.Run(() =>
        {
            var format = new WaveFormat(sampleRate, 16, 1);
            using var writer = new WaveFileWriter(filePath, format);

            // Convert float32 [-1,1] to 16-bit PCM bytes
            var buffer = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                var clamped = Math.Clamp(samples[i], -1f, 1f);
                short pcm = (short)(clamped * 32767f);
                buffer[i * 2] = (byte)(pcm & 0xFF);
                buffer[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
            }

            writer.Write(buffer, 0, buffer.Length);

            Trace.TraceInformation(
                "[AudioExportService] Exported WAV: {0} samples at {1}Hz to '{2}'",
                samples.Length, sampleRate, filePath);
        });
    }

    /// <summary>
    /// Exports audio samples to an MP3 file via MediaFoundationEncoder.
    /// Resamples to 44100Hz if needed (MediaFoundation may not support 24kHz MP3).
    /// </summary>
    public async Task ExportToMp3Async(float[] samples, int sampleRate, string filePath)
    {
        await Task.Run(() =>
        {
            const int targetRate = 44100;

            // Build a 16-bit PCM WaveFormat at original rate
            var sourceFormat = new WaveFormat(sampleRate, 16, 1);

            // Convert float32 to 16-bit PCM bytes
            var pcmBytes = FloatToPcm16Bytes(samples);

            using var sourceStream = new RawSourceWaveStream(
                new MemoryStream(pcmBytes), sourceFormat);

            IWaveProvider finalSource;
            MediaFoundationResampler? resampler = null;

            if (sampleRate != targetRate)
            {
                var targetFormat = new WaveFormat(targetRate, 16, 1);
                resampler = new MediaFoundationResampler(sourceStream, targetFormat)
                {
                    ResamplerQuality = 60
                };
                finalSource = resampler;
            }
            else
            {
                finalSource = sourceStream;
            }

            try
            {
                MediaFoundationEncoder.EncodeToMp3(finalSource, filePath);

                Trace.TraceInformation(
                    "[AudioExportService] Exported MP3: {0} samples at {1}Hz to '{2}'",
                    samples.Length, sampleRate, filePath);
            }
            finally
            {
                resampler?.Dispose();
            }
        });
    }

    /// <summary>
    /// Exports audio samples to an OGG/Opus file via Concentus.
    /// Resamples to 48kHz (Opus standard rate) if needed.
    /// </summary>
    public async Task ExportToOggAsync(float[] samples, int sampleRate, string filePath)
    {
        await Task.Run(() =>
        {
            const int opusRate = 48000;

            // Resample to 48kHz if needed (Opus standard)
            short[] pcm16;
            if (sampleRate != opusRate)
            {
                pcm16 = ResampleToInt16(samples, sampleRate, opusRate);
            }
            else
            {
                pcm16 = FloatToInt16(samples);
            }

            // Opus frame size: 20ms at 48kHz = 960 samples
            const int frameSize = 960;
            var encoder = OpusCodecFactory.CreateEncoder(opusRate, 1, Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO);

            using var fileStream = File.Create(filePath);
            var oggWriter = new OpusOggWriteStream(encoder, fileStream);

            // Write frames
            int offset = 0;
            while (offset + frameSize <= pcm16.Length)
            {
                var frame = new short[frameSize];
                Array.Copy(pcm16, offset, frame, 0, frameSize);
                oggWriter.WriteSamples(frame, 0, frameSize);
                offset += frameSize;
            }

            // Handle remaining samples by zero-padding the last frame
            if (offset < pcm16.Length)
            {
                int remaining = pcm16.Length - offset;
                var lastFrame = new short[frameSize];
                Array.Copy(pcm16, offset, lastFrame, 0, remaining);
                oggWriter.WriteSamples(lastFrame, 0, frameSize);
            }

            oggWriter.Finish();

            Trace.TraceInformation(
                "[AudioExportService] Exported OGG/Opus: {0} samples at {1}Hz to '{2}'",
                samples.Length, sampleRate, filePath);
        });
    }

    private static byte[] FloatToPcm16Bytes(float[] samples)
    {
        var buffer = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            var clamped = Math.Clamp(samples[i], -1f, 1f);
            short pcm = (short)(clamped * 32767f);
            buffer[i * 2] = (byte)(pcm & 0xFF);
            buffer[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
        }
        return buffer;
    }

    private static short[] FloatToInt16(float[] samples)
    {
        var result = new short[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            result[i] = (short)(Math.Clamp(samples[i], -1f, 1f) * 32767f);
        }
        return result;
    }

    /// <summary>
    /// Simple linear interpolation resampler from float32 to int16 at a new rate.
    /// </summary>
    private static short[] ResampleToInt16(float[] samples, int fromRate, int toRate)
    {
        double ratio = (double)fromRate / toRate;
        int outputLength = (int)(samples.Length / ratio);
        var result = new short[outputLength];

        for (int i = 0; i < outputLength; i++)
        {
            double srcIndex = i * ratio;
            int idx = (int)srcIndex;
            double frac = srcIndex - idx;

            float sample;
            if (idx + 1 < samples.Length)
            {
                sample = (float)(samples[idx] * (1 - frac) + samples[idx + 1] * frac);
            }
            else
            {
                sample = samples[Math.Min(idx, samples.Length - 1)];
            }

            result[i] = (short)(Math.Clamp(sample, -1f, 1f) * 32767f);
        }

        return result;
    }
}
