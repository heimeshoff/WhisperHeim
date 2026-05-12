using System.Diagnostics;
using System.IO;
using Concentus;
using Concentus.Oggfile;
using NAudio.Wave;
using WhisperHeim.Services.Ffmpeg;

namespace WhisperHeim.Services.FileTranscription;

/// <summary>
/// Decodes audio files (WAV, MP3, M4A, OGG) to 16kHz mono float32 PCM samples.
/// Uses NAudio's MediaFoundationReader for MP3/M4A/WAV (Windows Media Foundation),
/// and NAudio's built-in WaveFileReader for plain WAV files.
/// For OGG/Opus files (e.g. WhatsApp voice messages), uses Concentus managed decoder.
/// </summary>
internal static class AudioFileDecoder
{
    private const int TargetSampleRate = 16000;
    private const int TargetChannels = 1;
    private const int TargetBitsPerSample = 16;

    /// <summary>
    /// Decodes an audio file to 16kHz mono float32 PCM samples.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <returns>Tuple of (samples, sampleRate).</returns>
    /// <exception cref="InvalidOperationException">If the file cannot be decoded.</exception>
    public static (float[] Samples, int SampleRate) Decode(string filePath, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        try
        {
            return extension switch
            {
                ".wav" => DecodeWav(filePath),
                ".mp3" => DecodeWithMediaFoundation(filePath),
                ".m4a" => DecodeWithMediaFoundation(filePath),
                ".ogg" => DecodeOgg(filePath, cancellationToken),
                _ => throw new NotSupportedException(
                    $"Audio format '{extension}' is not supported. Supported formats: .wav, .mp3, .m4a, .ogg")
            };
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Trace.TraceError(
                "[AudioFileDecoder] Failed to decode '{0}': {1}", filePath, ex.Message);
            throw new InvalidOperationException(
                $"Failed to decode audio file '{Path.GetFileName(filePath)}': {ex.Message}", ex);
        }
    }

    private static (float[] Samples, int SampleRate) DecodeWav(string filePath)
    {
        // Try WaveFileReader first (more reliable for standard WAV)
        try
        {
            using var reader = new WaveFileReader(filePath);
            return ResampleAndConvert(reader);
        }
        catch
        {
            // Fall back to MediaFoundation for non-standard WAV
            return DecodeWithMediaFoundation(filePath);
        }
    }

    private static (float[] Samples, int SampleRate) DecodeOgg(string filePath, CancellationToken cancellationToken = default)
    {
        // Try ffmpeg first when available, but always fall back to Concentus
        // silently if ffmpeg is missing or fails. OGG decode must NOT block on
        // a UI dialog — file transcription has a working pure-managed path
        // (Concentus) that handles the WhatsApp / common-OGG case fine.
        //
        // Resolution: prefer the FfmpegDetector's cached path if a detector
        // was injected via SetDetector (App startup wires this up). Otherwise
        // try plain "ffmpeg" on PATH. Either way, any failure short-circuits
        // to Concentus.
        var ffmpegPath = _detector?.CachedInfo?.ExecutablePath;
        if (ffmpegPath is not null || _detector is null)
        {
            try
            {
                var result = DecodeOggWithFfmpeg(filePath, ffmpegPath ?? "ffmpeg", cancellationToken);
                if (result.Samples.Length > 0)
                    return result;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("[AudioFileDecoder] ffmpeg OGG decode failed, falling back to Concentus: {0}", ex.Message);
            }
        }
        else
        {
            Trace.TraceInformation("[AudioFileDecoder] FFmpeg not detected; using Concentus for OGG.");
        }

        return DecodeOggWithConcentus(filePath, cancellationToken);
    }

    /// <summary>
    /// Optional process-wide FFmpeg detector. Set once at app startup so the
    /// OGG decoder uses the detected absolute path (covering the
    /// winget-installed location even when PATH hasn't refreshed).
    ///
    /// <para>
    /// Per Task 110 contract: <see cref="DecodeOgg"/> keeps its Concentus
    /// fallback ahead of any modal prompt. We never call
    /// <see cref="IFfmpegPromptService"/> from here — file transcription
    /// must not block on a UI dialog.
    /// </para>
    /// </summary>
    private static FfmpegDetector? _detector;

    public static void SetDetector(FfmpegDetector detector) => _detector = detector;

    private static (float[] Samples, int SampleRate) DecodeOggWithFfmpeg(string filePath, string ffmpegExe, CancellationToken cancellationToken)
    {
        // Decode to 16kHz mono 16-bit PCM via ffmpeg
        var tempFile = Path.GetTempFileName();
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpegExe,
                Arguments = $"-i \"{filePath}\" -ar {TargetSampleRate} -ac {TargetChannels} -f s16le -y \"{tempFile}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };

            using var process = System.Diagnostics.Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start ffmpeg");

            // Register cancellation to kill the process
            using var reg = cancellationToken.Register(() =>
            {
                try { process.Kill(); } catch { /* ignore */ }
            });

            process.WaitForExit(60_000); // 60 second timeout
            cancellationToken.ThrowIfCancellationRequested();

            if (!process.HasExited)
            {
                process.Kill();
                throw new TimeoutException("ffmpeg timed out decoding OGG file");
            }

            if (process.ExitCode != 0)
            {
                var stderr = process.StandardError.ReadToEnd();
                throw new InvalidOperationException($"ffmpeg exited with code {process.ExitCode}: {stderr}");
            }

            // Read raw PCM bytes and convert to float32
            var rawBytes = File.ReadAllBytes(tempFile);
            int sampleCount = rawBytes.Length / 2;
            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                short sample = BitConverter.ToInt16(rawBytes, i * 2);
                samples[i] = sample / 32768f;
            }

            Trace.TraceInformation(
                "[AudioFileDecoder] Decoded OGG/Opus via ffmpeg: {0} samples ({1:F2}s) at {2}Hz",
                samples.Length, (double)samples.Length / TargetSampleRate, TargetSampleRate);

            return (samples, TargetSampleRate);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { /* ignore */ }
        }
    }

    private static (float[] Samples, int SampleRate) DecodeOggWithConcentus(string filePath, CancellationToken cancellationToken)
    {
        // Fallback: Concentus managed Opus decoder
        using var fileStream = File.OpenRead(filePath);
        var fileLength = fileStream.Length;
        var decoder = OpusCodecFactory.CreateDecoder(48000, 1);
        var oggReader = new OpusOggReadStream(decoder, fileStream);

        var allSamples = new List<short>();
        int consecutiveNulls = 0;
        while (oggReader.HasNextPacket)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var packet = oggReader.DecodeNextPacket();
            if (packet != null && packet.Length > 0)
            {
                allSamples.AddRange(packet);
                consecutiveNulls = 0;
            }
            else
            {
                consecutiveNulls++;
                // Concentus bug: HasNextPacket returns true forever after EOF
                if (consecutiveNulls >= 100 && fileStream.Position >= fileLength)
                {
                    Trace.TraceInformation(
                        "[AudioFileDecoder] OGG stream ended (EOF + null packets). Total: {0} samples.",
                        allSamples.Count);
                    break;
                }
            }
        }

        if (allSamples.Count == 0)
            return (Array.Empty<float>(), TargetSampleRate);

        int ratio = 48000 / TargetSampleRate;
        int outputLength = allSamples.Count / ratio;
        var samples = new float[outputLength];
        for (int i = 0; i < outputLength; i++)
        {
            samples[i] = allSamples[i * ratio] / 32768f;
        }

        Trace.TraceInformation(
            "[AudioFileDecoder] Decoded OGG/Opus via Concentus: {0} samples ({1:F2}s) at {2}Hz",
            samples.Length, (double)samples.Length / TargetSampleRate, TargetSampleRate);

        return (samples, TargetSampleRate);
    }

    private static (float[] Samples, int SampleRate) DecodeWithMediaFoundation(string filePath)
    {
        using var reader = new MediaFoundationReader(filePath);
        return ResampleAndConvert(reader);
    }

    /// <summary>
    /// Takes any WaveStream, resamples to 16kHz mono 16-bit, then converts to float32.
    /// </summary>
    private static (float[] Samples, int SampleRate) ResampleAndConvert(WaveStream source)
    {
        var targetFormat = new WaveFormat(TargetSampleRate, TargetBitsPerSample, TargetChannels);

        // If source is already in the target format, read directly
        IWaveProvider resampled;
        bool disposeResampled = false;

        if (source.WaveFormat.SampleRate == TargetSampleRate &&
            source.WaveFormat.Channels == TargetChannels &&
            source.WaveFormat.BitsPerSample == TargetBitsPerSample &&
            source.WaveFormat.Encoding == WaveFormatEncoding.Pcm)
        {
            resampled = source;
        }
        else
        {
            resampled = new MediaFoundationResampler(source, targetFormat)
            {
                ResamplerQuality = 60 // High quality resampling
            };
            disposeResampled = true;
        }

        try
        {
            return ReadAsFloat32(resampled);
        }
        finally
        {
            if (disposeResampled && resampled is IDisposable disposable)
                disposable.Dispose();
        }
    }

    /// <summary>
    /// Reads 16-bit PCM from a WaveStream and converts to float32 in [-1.0, 1.0].
    /// </summary>
    private static (float[] Samples, int SampleRate) ReadAsFloat32(IWaveProvider stream)
    {
        // Read all bytes
        using var memoryStream = new MemoryStream();
        byte[] buffer = new byte[8192];
        int bytesRead;

        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            memoryStream.Write(buffer, 0, bytesRead);
        }

        byte[] allBytes = memoryStream.ToArray();

        // Convert 16-bit PCM to float32
        int sampleCount = allBytes.Length / 2; // 2 bytes per 16-bit sample
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            short sample = BitConverter.ToInt16(allBytes, i * 2);
            samples[i] = sample / 32768f;
        }

        Trace.TraceInformation(
            "[AudioFileDecoder] Decoded {0} samples ({1:F2}s) at {2}Hz",
            sampleCount,
            (double)sampleCount / TargetSampleRate,
            TargetSampleRate);

        return (samples, TargetSampleRate);
    }
}
