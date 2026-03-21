using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace WhisperHeim.Services.Audio;

/// <summary>
/// Voice Activity Detection service using the Silero VAD ONNX model.
/// Processes audio chunks, maintains ONNX hidden state, and fires
/// SpeechStarted / SpeechEnded events with accumulated speech audio.
/// </summary>
public sealed class SileroVadService : IVoiceActivityDetector
{
    private const int HiddenSize = 64;
    private const int NumLayers = 2;

    private readonly VadSettings _settings;
    private readonly InferenceSession _session;
    private readonly object _lock = new();

    // ONNX recurrent hidden states
    private float[] _h;
    private float[] _c;

    // Internal accumulation buffer for incoming audio
    private readonly List<float> _pendingSamples = new();

    // Speech segment accumulator
    private readonly List<float> _speechBuffer = new();

    // Pre-speech ring buffer for padding
    private readonly float[] _preSpeechRing;
    private int _preSpeechWritePos;
    private int _preSpeechCount;

    // State tracking
    private bool _inSpeech;
    private int _speechFrameCount;
    private int _silenceFrameCount;
    private bool _speechStartedFired;
    private bool _disposed;

    // Derived constants
    private readonly int _minSpeechFrames;
    private readonly int _minSilenceFrames;
    private readonly float _frameDurationMs;

    public SileroVadService(string modelPath, VadSettings? settings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);

        _settings = settings ?? new VadSettings();

        ValidateSettings(_settings);

        var sessionOptions = new SessionOptions
        {
            InterOpNumThreads = 1,
            IntraOpNumThreads = 1,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
        };

        _session = new InferenceSession(modelPath, sessionOptions);

        _h = new float[NumLayers * 1 * HiddenSize];
        _c = new float[NumLayers * 1 * HiddenSize];

        // Calculate frame duration and minimum frame counts
        _frameDurationMs = _settings.ChunkSamples * 1000f / _settings.SampleRate;
        _minSpeechFrames = Math.Max(1, (int)Math.Ceiling(_settings.MinSpeechDurationMs / _frameDurationMs));
        _minSilenceFrames = Math.Max(1, (int)Math.Ceiling(_settings.MinSilenceDurationMs / _frameDurationMs));

        // Pre-speech padding ring buffer
        int preSpeechSamples = (int)(_settings.PreSpeechPadMs / 1000.0 * _settings.SampleRate);
        _preSpeechRing = new float[Math.Max(1, preSpeechSamples)];
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
            {
                return _inSpeech && _speechStartedFired;
            }
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

            // Process all complete chunks
            while (_pendingSamples.Count >= _settings.ChunkSamples)
            {
                float[] chunk = new float[_settings.ChunkSamples];
                _pendingSamples.CopyTo(0, chunk, 0, _settings.ChunkSamples);
                _pendingSamples.RemoveRange(0, _settings.ChunkSamples);

                float probability = RunInference(chunk);
                UpdateState(probability, chunk);
            }
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (_lock)
        {
            Array.Clear(_h);
            Array.Clear(_c);
            _pendingSamples.Clear();
            _speechBuffer.Clear();
            Array.Clear(_preSpeechRing);
            _preSpeechWritePos = 0;
            _preSpeechCount = 0;
            _inSpeech = false;
            _speechFrameCount = 0;
            _silenceFrameCount = 0;
            _speechStartedFired = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // If speech was in progress, emit what we have
        lock (_lock)
        {
            if (_speechStartedFired && _speechBuffer.Count > 0)
            {
                EmitSpeechEnded();
            }
        }

        _session.Dispose();
    }

    /// <summary>
    /// Runs a single inference pass on a chunk of audio samples.
    /// Returns the speech probability [0.0, 1.0].
    /// </summary>
    private float RunInference(float[] chunk)
    {
        // Input tensor: (1, chunkSamples)
        var inputTensor = new DenseTensor<float>(
            chunk, new[] { 1, _settings.ChunkSamples });

        // Sample rate tensor: scalar int64
        var srTensor = new DenseTensor<long>(
            new[] { (long)_settings.SampleRate }, new[] { 1 });

        // Hidden state tensors: (numLayers, 1, hiddenSize)
        var hTensor = new DenseTensor<float>(
            _h, new[] { NumLayers, 1, HiddenSize });
        var cTensor = new DenseTensor<float>(
            _c, new[] { NumLayers, 1, HiddenSize });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", inputTensor),
            NamedOnnxValue.CreateFromTensor("sr", srTensor),
            NamedOnnxValue.CreateFromTensor("h", hTensor),
            NamedOnnxValue.CreateFromTensor("c", cTensor),
        };

        using var results = _session.Run(inputs);

        float probability = 0f;

        foreach (var result in results)
        {
            if (result.Name == "output")
            {
                var outputTensor = result.AsTensor<float>();
                probability = outputTensor.GetValue(0);
            }
            else if (result.Name == "hn")
            {
                var hnTensor = result.AsTensor<float>();
                for (int i = 0; i < _h.Length; i++) _h[i] = hnTensor.GetValue(i);
            }
            else if (result.Name == "cn")
            {
                var cnTensor = result.AsTensor<float>();
                for (int i = 0; i < _c.Length; i++) _c[i] = cnTensor.GetValue(i);
            }
        }

        return probability;
    }

    /// <summary>
    /// Updates the speech/silence state machine based on the probability of the current frame.
    /// </summary>
    private void UpdateState(float probability, float[] chunk)
    {
        bool isSpeechFrame = probability >= _settings.SpeechThreshold;
        bool isSilenceFrame = probability < _settings.SilenceThreshold;

        if (!_inSpeech)
        {
            // Not currently in a speech segment
            WritePreSpeechRing(chunk);

            if (isSpeechFrame)
            {
                _speechFrameCount++;

                if (_speechFrameCount >= _minSpeechFrames)
                {
                    // Confirmed speech start
                    _inSpeech = true;
                    _silenceFrameCount = 0;

                    // Prepend pre-speech padding
                    PrependPreSpeechAudio();

                    // Add the current chunk
                    _speechBuffer.AddRange(chunk);

                    _speechStartedFired = true;
                    SpeechStarted?.Invoke(this, EventArgs.Empty);
                }
            }
            else
            {
                _speechFrameCount = 0;
            }
        }
        else
        {
            // Currently in a speech segment - accumulate audio
            _speechBuffer.AddRange(chunk);

            if (isSilenceFrame)
            {
                _silenceFrameCount++;

                if (_silenceFrameCount >= _minSilenceFrames)
                {
                    // Speech has ended
                    EmitSpeechEnded();

                    _inSpeech = false;
                    _speechFrameCount = 0;
                    _silenceFrameCount = 0;
                    _speechStartedFired = false;
                }
            }
            else
            {
                _silenceFrameCount = 0;
            }
        }
    }

    private void WritePreSpeechRing(float[] chunk)
    {
        for (int i = 0; i < chunk.Length; i++)
        {
            _preSpeechRing[_preSpeechWritePos % _preSpeechRing.Length] = chunk[i];
            _preSpeechWritePos++;
        }

        _preSpeechCount = Math.Min(_preSpeechCount + chunk.Length, _preSpeechRing.Length);
    }

    private void PrependPreSpeechAudio()
    {
        if (_preSpeechCount == 0)
            return;

        int startIdx = (_preSpeechWritePos - _preSpeechCount + _preSpeechRing.Length * 2)
                        % _preSpeechRing.Length;

        for (int i = 0; i < _preSpeechCount; i++)
        {
            _speechBuffer.Add(_preSpeechRing[(startIdx + i) % _preSpeechRing.Length]);
        }

        // Reset pre-speech ring
        _preSpeechCount = 0;
        _preSpeechWritePos = 0;
    }

    private void EmitSpeechEnded()
    {
        if (_speechBuffer.Count == 0)
            return;

        float[] speechAudio = _speechBuffer.ToArray();
        _speechBuffer.Clear();

        SpeechEnded?.Invoke(this, new SpeechEndedEventArgs(speechAudio));
    }

    private static void ValidateSettings(VadSettings settings)
    {
        if (settings.SpeechThreshold is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(settings), "SpeechThreshold must be in [0.0, 1.0].");

        if (settings.SilenceThreshold is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(settings), "SilenceThreshold must be in [0.0, 1.0].");

        if (settings.ChunkSamples is not (512 or 1536))
            throw new ArgumentOutOfRangeException(nameof(settings), "ChunkSamples must be 512 or 1536 for Silero VAD at 16kHz.");

        if (settings.SampleRate != 16000)
            throw new ArgumentOutOfRangeException(nameof(settings), "SampleRate must be 16000 for Silero VAD.");

        if (settings.MinSpeechDurationMs < 0)
            throw new ArgumentOutOfRangeException(nameof(settings), "MinSpeechDurationMs must be >= 0.");

        if (settings.MinSilenceDurationMs < 0)
            throw new ArgumentOutOfRangeException(nameof(settings), "MinSilenceDurationMs must be >= 0.");
    }
}
