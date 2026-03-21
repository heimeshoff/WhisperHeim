using System.Diagnostics;
using SherpaOnnx;

namespace WhisperHeim.Services.Audio;

/// <summary>
/// Voice Activity Detection service using sherpa-onnx's built-in Silero VAD wrapper.
/// Processes audio chunks and fires SpeechStarted / SpeechEnded events with accumulated speech audio.
/// </summary>
public sealed class SileroVadService : IVoiceActivityDetector
{
    private readonly VadSettings _settings;
    private readonly VoiceActivityDetector _vad;
    private readonly int _windowSize;
    private readonly object _lock = new();

    // Internal accumulation buffer for incoming audio (to feed exact window-sized chunks)
    private readonly List<float> _pendingSamples = new();

    // State tracking
    private bool _wasSpeech;
    private bool _disposed;

    public SileroVadService(string modelPath, VadSettings? settings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);

        _settings = settings ?? new VadSettings();
        _windowSize = _settings.ChunkSamples;

        var config = new VadModelConfig();
        config.SileroVad.Model = modelPath;
        config.SileroVad.Threshold = _settings.SpeechThreshold;
        config.SileroVad.MinSilenceDuration = _settings.MinSilenceDurationMs / 1000f;
        config.SileroVad.MinSpeechDuration = _settings.MinSpeechDurationMs / 1000f;
        config.SileroVad.MaxSpeechDuration = 30f; // 30 seconds max before forced segment split
        config.SileroVad.WindowSize = _windowSize;
        config.SampleRate = _settings.SampleRate;
        config.NumThreads = 1;
        config.Debug = 0;

        _vad = new VoiceActivityDetector(config, bufferSizeInSeconds: 60);

        Trace.TraceInformation(
            "[SileroVAD] Initialized via sherpa-onnx. window={0}, threshold={1:F2}, sampleRate={2}",
            _windowSize, _settings.SpeechThreshold, _settings.SampleRate);
    }

    /// <inheritdoc />
    public event EventHandler? SpeechStarted;

    /// <inheritdoc />
    public event EventHandler<SpeechEndedEventArgs>? SpeechEnded;

    /// <inheritdoc />
    public bool IsSpeechDetected
    {
        get
        {
            lock (_lock)
                return _wasSpeech;
        }
    }

    /// <inheritdoc />
    public void ProcessAudio(float[] samples)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (samples.Length == 0)
            return;

        lock (_lock)
        {
            _pendingSamples.AddRange(samples);

            // Feed exact window-sized chunks to the VAD
            while (_pendingSamples.Count >= _windowSize)
            {
                float[] chunk = new float[_windowSize];
                _pendingSamples.CopyTo(0, chunk, 0, _windowSize);
                _pendingSamples.RemoveRange(0, _windowSize);

                _vad.AcceptWaveform(chunk);

                bool isSpeech = _vad.IsSpeechDetected();

                // Detect speech start transition
                if (isSpeech && !_wasSpeech)
                {
                    _wasSpeech = true;
                    Trace.TraceInformation("[SileroVAD] Speech started.");
                    SpeechStarted?.Invoke(this, EventArgs.Empty);
                }

                // Check for completed speech segments
                while (!_vad.IsEmpty())
                {
                    var segment = _vad.Front();
                    _vad.Pop();

                    _wasSpeech = false;

                    Trace.TraceInformation(
                        "[SileroVAD] Speech ended. Segment: start={0}, samples={1} ({2:F2}s)",
                        segment.Start,
                        segment.Samples.Length,
                        segment.Samples.Length / (float)_settings.SampleRate);

                    SpeechEnded?.Invoke(this, new SpeechEndedEventArgs(segment.Samples));
                }
            }
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (_lock)
        {
            _pendingSamples.Clear();
            _wasSpeech = false;
            _vad.Reset();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        lock (_lock)
        {
            // Flush any remaining audio
            _vad.Flush();
            while (!_vad.IsEmpty())
            {
                var segment = _vad.Front();
                _vad.Pop();
                SpeechEnded?.Invoke(this, new SpeechEndedEventArgs(segment.Samples));
            }
        }

        _vad.Dispose();
    }
}
