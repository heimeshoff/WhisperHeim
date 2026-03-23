using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace WhisperHeim.Services.Audio;

/// <summary>
/// Captures system audio (loopback) via WASAPI loopback recording using NAudio's
/// WasapiLoopbackCapture. Resamples to 16kHz mono float32 for the ASR pipeline
/// and writes raw audio to a temporary WAV file during recording.
/// </summary>
public sealed class LoopbackCaptureService : IAudioCaptureService
{
    /// <summary>16kHz sample rate as required by Whisper models.</summary>
    public const int TargetSampleRate = 16000;

    /// <summary>Mono channel.</summary>
    public const int TargetChannels = 1;

    /// <summary>Ring buffer holds 30 seconds of audio at 16kHz.</summary>
    private const int RingBufferSeconds = 30;

    private readonly AudioRingBuffer _ringBuffer;
    private WasapiLoopbackCapture? _capture;
    private WaveFileWriter? _waveFileWriter;
    private string? _tempWavFilePath;
    private bool _disposed;
    private volatile bool _isCapturing;

    public LoopbackCaptureService()
    {
        _ringBuffer = new AudioRingBuffer(TargetSampleRate * RingBufferSeconds);
    }

    /// <inheritdoc />
    public event EventHandler<AudioDataEventArgs>? AudioDataAvailable;

    /// <inheritdoc />
    public event EventHandler? CaptureStarted;

    /// <inheritdoc />
    public event EventHandler<CaptureStoppedEventArgs>? CaptureStopped;

    /// <inheritdoc />
    public bool IsCapturing => _isCapturing;

    /// <summary>
    /// Provides direct access to the ring buffer for consumers that want to
    /// pull samples on their own schedule rather than relying on events.
    /// </summary>
    public AudioRingBuffer RingBuffer => _ringBuffer;

    /// <summary>
    /// Gets the path to the temporary WAV file written during the last (or current) recording.
    /// Returns null if no recording has been started.
    /// </summary>
    public string? TempWavFilePath => _tempWavFilePath;

    /// <summary>
    /// Gets or sets the output WAV file path. If set before <see cref="StartCapture"/>,
    /// the recording will be written directly to this path instead of a temp file.
    /// </summary>
    public string? OutputFilePath { get; set; }

    /// <inheritdoc />
    /// <remarks>
    /// For loopback capture, there is typically only one render device (the default output).
    /// We enumerate active render endpoints via CoreAudioApi.
    /// </remarks>
    public IReadOnlyList<AudioDeviceInfo> GetAvailableDevices()
    {
        var devices = new List<AudioDeviceInfo>();

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            for (int i = 0; i < endpoints.Count; i++)
            {
                var endpoint = endpoints[i];
                devices.Add(new AudioDeviceInfo(
                    i,
                    endpoint.FriendlyName,
                    endpoint.AudioClient.MixFormat.Channels));
            }
        }
        catch (Exception)
        {
            // No audio devices available or COM error -- return empty list.
        }

        return devices;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The <paramref name="deviceIndex"/> is ignored for loopback capture because
    /// WasapiLoopbackCapture always captures from the default render device.
    /// </remarks>
    public void StartCapture(int deviceIndex = -1)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isCapturing)
            return;

        // Verify that a render device is available
        MMDevice? renderDevice = null;
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            renderDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "No audio output device is available for loopback capture.", ex);
        }

        _ringBuffer.Clear();

        // Use caller-supplied output path, or fall back to a temp file
        if (!string.IsNullOrEmpty(OutputFilePath))
        {
            _tempWavFilePath = OutputFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(_tempWavFilePath)!);
        }
        else
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "WhisperHeim");
            Directory.CreateDirectory(tempDir);
            _tempWavFilePath = Path.Combine(
                tempDir,
                $"call_loopback_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.wav");
        }

        _capture = new WasapiLoopbackCapture();
        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;

        // Open WAV file writer with the target 16kHz mono 32-bit float format
        var targetFormat = WaveFormat.CreateIeeeFloatWaveFormat(TargetSampleRate, TargetChannels);
        _waveFileWriter = new WaveFileWriter(_tempWavFilePath, targetFormat);

        try
        {
            _capture.StartRecording();
            _isCapturing = true;
            CaptureStarted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
            CleanupCapture();
            throw;
        }
    }

    /// <inheritdoc />
    public void StopCapture()
    {
        if (!_isCapturing)
            return;

        _isCapturing = false;

        try
        {
            _capture?.StopRecording();
        }
        catch (Exception)
        {
            // StopRecording may throw if device was already disconnected.
            // The RecordingStopped event handler will deal with cleanup.
            CleanupCapture();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopCapture();
        CleanupCapture();
    }

    /// <summary>
    /// Called on the WASAPI capture thread when audio data is available.
    /// The source format is the system mixer format (typically 32-bit float, 48kHz, stereo).
    /// We resample to 16kHz mono float32.
    /// </summary>
    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        try
        {
            int bytesRecorded = e.BytesRecorded;
            if (bytesRecorded == 0 || _capture is null)
                return;

            WaveFormat sourceFormat = _capture.WaveFormat;

            // Convert raw bytes to float samples based on source format
            float[] sourceFloats = ConvertToFloat(e.Buffer, bytesRecorded, sourceFormat);

            // Down-mix to mono if needed
            float[] monoSamples = sourceFormat.Channels > 1
                ? DownmixToMono(sourceFloats, sourceFormat.Channels)
                : sourceFloats;

            // Resample from source sample rate to 16kHz
            float[] resampled = sourceFormat.SampleRate != TargetSampleRate
                ? Resample(monoSamples, sourceFormat.SampleRate, TargetSampleRate)
                : monoSamples;

            // Write to WAV file
            _waveFileWriter?.WriteSamples(resampled, 0, resampled.Length);

            // Push into ring buffer
            _ringBuffer.Write(resampled);

            // Raise event for subscribers
            AudioDataAvailable?.Invoke(this, new AudioDataEventArgs(resampled));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("[LoopbackCapture] OnDataAvailable error: {0}", ex);
        }
    }

    /// <summary>
    /// Called when WASAPI stops recording (user stop or device disconnection).
    /// </summary>
    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        try
        {
            _isCapturing = false;

            bool deviceDisconnected = e.Exception is not null;

            CleanupCapture();

            CaptureStopped?.Invoke(this, new CaptureStoppedEventArgs(
                wasDeviceDisconnected: deviceDisconnected,
                exception: e.Exception));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("[LoopbackCapture] OnRecordingStopped error: {0}", ex);
        }
    }

    /// <summary>
    /// Converts raw PCM bytes to float samples. Handles IEEE float and 16-bit PCM source formats.
    /// </summary>
    private static float[] ConvertToFloat(byte[] buffer, int bytesRecorded, WaveFormat format)
    {
        if (format.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            int sampleCount = bytesRecorded / 4;
            float[] samples = new float[sampleCount];
            Buffer.BlockCopy(buffer, 0, samples, 0, bytesRecorded);
            return samples;
        }

        if (format.Encoding == WaveFormatEncoding.Pcm && format.BitsPerSample == 16)
        {
            int sampleCount = bytesRecorded / 2;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                short pcm16 = BitConverter.ToInt16(buffer, i * 2);
                samples[i] = pcm16 / 32768f;
            }
            return samples;
        }

        if (format.Encoding == WaveFormatEncoding.Pcm && format.BitsPerSample == 32)
        {
            int sampleCount = bytesRecorded / 4;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                int pcm32 = BitConverter.ToInt32(buffer, i * 4);
                samples[i] = pcm32 / 2147483648f;
            }
            return samples;
        }

        // Fallback for extensible format (typically IEEE float underneath)
        if (format.Encoding == WaveFormatEncoding.Extensible && format.BitsPerSample == 32)
        {
            int sampleCount = bytesRecorded / 4;
            float[] samples = new float[sampleCount];
            Buffer.BlockCopy(buffer, 0, samples, 0, bytesRecorded);
            return samples;
        }

        throw new NotSupportedException(
            $"Unsupported audio format: {format.Encoding}, {format.BitsPerSample}-bit");
    }

    /// <summary>
    /// Down-mixes interleaved multi-channel audio to mono by averaging all channels.
    /// </summary>
    private static float[] DownmixToMono(float[] samples, int channels)
    {
        int frameCount = samples.Length / channels;
        float[] mono = new float[frameCount];

        for (int i = 0; i < frameCount; i++)
        {
            float sum = 0f;
            int offset = i * channels;
            for (int ch = 0; ch < channels; ch++)
            {
                sum += samples[offset + ch];
            }
            mono[i] = sum / channels;
        }

        return mono;
    }

    /// <summary>
    /// Simple linear interpolation resampler. Sufficient for downsampling from
    /// common rates (44.1kHz, 48kHz) to 16kHz for speech recognition.
    /// </summary>
    private static float[] Resample(float[] source, int sourceRate, int targetRate)
    {
        if (source.Length == 0)
            return [];

        double ratio = (double)sourceRate / targetRate;
        int outputLength = (int)(source.Length / ratio);
        float[] output = new float[outputLength];

        for (int i = 0; i < outputLength; i++)
        {
            double srcIndex = i * ratio;
            int srcIndexInt = (int)srcIndex;
            double frac = srcIndex - srcIndexInt;

            if (srcIndexInt + 1 < source.Length)
            {
                output[i] = (float)(source[srcIndexInt] * (1.0 - frac)
                                  + source[srcIndexInt + 1] * frac);
            }
            else
            {
                output[i] = source[srcIndexInt];
            }
        }

        return output;
    }

    private void CleanupCapture()
    {
        if (_capture is not null)
        {
            _capture.DataAvailable -= OnDataAvailable;
            _capture.RecordingStopped -= OnRecordingStopped;
            _capture.Dispose();
            _capture = null;
        }

        if (_waveFileWriter is not null)
        {
            try
            {
                _waveFileWriter.Dispose();
            }
            catch (Exception)
            {
                // Ignore disposal errors for file writer.
            }
            _waveFileWriter = null;
        }
    }
}
