using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using NAudio.Wave;
using SherpaOnnx;
using WhisperHeim.Services.Models;

namespace WhisperHeim.Services.TextToSpeech;

/// <summary>
/// Generates speech audio from text using Kyutai Pocket TTS via sherpa-onnx.
/// Audio playback uses NAudio WaveOutEvent at 24kHz (Pocket TTS native sample rate).
/// </summary>
public sealed class TextToSpeechService : ITextToSpeechService
{
    /// <summary>Pocket TTS outputs at 24kHz.</summary>
    private const int PocketTtsSampleRate = 24000;

    /// <summary>Maximum reference audio length in seconds for voice cloning.</summary>
    private const int MaxReferenceAudioLenSeconds = 12;

    private OfflineTts? _tts;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Directory for custom voice reference .wav files.
    /// Users can drop .wav files here to add custom voices.
    /// </summary>
    private static readonly string CustomVoicesDir =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WhisperHeim",
            "voices");

    /// <inheritdoc />
    public bool IsLoaded => _tts is not null;

    /// <inheritdoc />
    public void LoadModel()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_tts is not null)
            return;

        var modelDir = ModelManagerService.GetModelDirectory(ModelManagerService.PocketTtsInt8);

        var lmFlowPath = Path.Combine(modelDir, "lm_flow.int8.onnx");
        var lmMainPath = Path.Combine(modelDir, "lm_main.int8.onnx");
        var encoderPath = Path.Combine(modelDir, "encoder.onnx");
        var decoderPath = Path.Combine(modelDir, "decoder.int8.onnx");
        var textConditionerPath = Path.Combine(modelDir, "text_conditioner.onnx");
        var vocabPath = Path.Combine(modelDir, "vocab.json");
        var tokenScoresPath = Path.Combine(modelDir, "token_scores.json");

        ValidateModelFile(lmFlowPath, "lm_flow");
        ValidateModelFile(lmMainPath, "lm_main");
        ValidateModelFile(encoderPath, "encoder");
        ValidateModelFile(decoderPath, "decoder");
        ValidateModelFile(textConditionerPath, "text_conditioner");
        ValidateModelFile(vocabPath, "vocab.json");
        ValidateModelFile(tokenScoresPath, "token_scores.json");

        var config = new OfflineTtsConfig();
        config.Model.Pocket.LmFlow = lmFlowPath;
        config.Model.Pocket.LmMain = lmMainPath;
        config.Model.Pocket.Encoder = encoderPath;
        config.Model.Pocket.Decoder = decoderPath;
        config.Model.Pocket.TextConditioner = textConditionerPath;
        config.Model.Pocket.VocabJson = vocabPath;
        config.Model.Pocket.TokenScoresJson = tokenScoresPath;

        config.Model.NumThreads = Math.Max(1, Environment.ProcessorCount / 2);
        config.Model.Debug = 0;
        config.Model.Provider = "cpu";

        Trace.TraceInformation("[TTS] Loading Pocket TTS model from {0}...", modelDir);
        _tts = new OfflineTts(config);
        Trace.TraceInformation("[TTS] Pocket TTS model loaded. SampleRate={0}", _tts.SampleRate);
    }

    /// <inheritdoc />
    public IReadOnlyList<TtsVoice> GetAvailableVoices()
    {
        var voices = new List<TtsVoice>();

        // Built-in voices from the model's test_wavs directory
        var modelDir = ModelManagerService.GetModelDirectory(ModelManagerService.PocketTtsInt8);
        var testWavsDir = Path.Combine(modelDir, "test_wavs");

        if (Directory.Exists(testWavsDir))
        {
            foreach (var wavFile in Directory.GetFiles(testWavsDir, "*.wav"))
            {
                var name = Path.GetFileNameWithoutExtension(wavFile);
                voices.Add(new TtsVoice(
                    Id: $"builtin:{name}",
                    DisplayName: char.ToUpper(name[0]) + name[1..],
                    ReferenceAudioPath: wavFile,
                    IsBuiltIn: true));
            }
        }

        // Custom voices from the user's voices directory
        if (Directory.Exists(CustomVoicesDir))
        {
            foreach (var wavFile in Directory.GetFiles(CustomVoicesDir, "*.wav"))
            {
                var name = Path.GetFileNameWithoutExtension(wavFile);
                voices.Add(new TtsVoice(
                    Id: $"custom:{name}",
                    DisplayName: name,
                    ReferenceAudioPath: wavFile,
                    IsBuiltIn: false));
            }
        }

        return voices;
    }

    /// <inheritdoc />
    public Task<TtsGenerationResult> GenerateAudioAsync(
        string text,
        string voiceId,
        float speed = 1.0f,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_tts is null)
            throw new InvalidOperationException("TTS model not loaded. Call LoadModel() first.");

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var genConfig = CreateGenerationConfig(voiceId, speed);
            var sw = Stopwatch.StartNew();

            // Use a no-op callback that supports cancellation instead of null
            // (null delegate can cause native crash in some sherpa-onnx versions)
            OfflineTtsCallbackProgressWithArg callback = (IntPtr samples, int n, float progress, IntPtr arg) =>
            {
                return cancellationToken.IsCancellationRequested ? 0 : 1;
            };

            var audio = _tts.GenerateWithConfig(text, genConfig, callback);
            GC.KeepAlive(callback);
            sw.Stop();

            var samples = audio.Samples;
            Trace.TraceInformation(
                "[TTS] Generated {0} samples ({1:F1}s audio) in {2:F1}s",
                samples.Length,
                (double)samples.Length / PocketTtsSampleRate,
                sw.Elapsed.TotalSeconds);

            return new TtsGenerationResult(samples, audio.SampleRate);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task GenerateAudioStreamingAsync(
        string text,
        string voiceId,
        Action<float[], float> onChunk,
        float speed = 1.0f,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_tts is null)
            throw new InvalidOperationException("TTS model not loaded. Call LoadModel() first.");

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var genConfig = CreateGenerationConfig(voiceId, speed);

            // IMPORTANT: The delegate must be prevented from garbage collection while
            // native code holds a reference to it. Store in a local and use GC.KeepAlive
            // after the native call completes.
            OfflineTtsCallbackProgressWithArg callback = (IntPtr samples, int n, float progress, IntPtr arg) =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                        return 0; // Stop generating

                    if (n > 0)
                    {
                        var chunk = new float[n];
                        Marshal.Copy(samples, chunk, 0, n);
                        onChunk(chunk, progress);
                    }

                    return 1; // Continue generating
                }
                catch (Exception ex)
                {
                    Trace.TraceError("[TTS] Error in streaming callback: {0}", ex);
                    return 0; // Stop on error
                }
            };

            _tts.GenerateWithConfig(text, genConfig, callback);

            // Prevent GC from collecting the delegate while native code is using it
            GC.KeepAlive(callback);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SpeakAsync(
        string text,
        string voiceId,
        float speed = 1.0f,
        int playbackDeviceNumber = -1,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_tts is null)
            throw new InvalidOperationException("TTS model not loaded. Call LoadModel() first.");

        var playbackComplete = new TaskCompletionSource();

        // Set up NAudio playback with IEEE float format (sherpa-onnx outputs float32 samples in [-1,1])
        using var waveOut = new WaveOutEvent { DeviceNumber = playbackDeviceNumber };
        var floatFormat = WaveFormat.CreateIeeeFloatWaveFormat(PocketTtsSampleRate, 1);
        var waveProvider = new BufferedWaveProvider(floatFormat)
        {
            // 30 seconds buffer to avoid overflow on longer texts
            BufferLength = PocketTtsSampleRate * 4 * 30,
            DiscardOnBufferOverflow = true
        };

        waveOut.Init(waveProvider);
        waveOut.PlaybackStopped += (_, args) =>
        {
            if (args.Exception is not null)
                Trace.TraceError("[TTS] Playback error: {0}", args.Exception);
            playbackComplete.TrySetResult();
        };

        var playbackStarted = false;

        // Start streaming generation in background
        var generationTask = GenerateAudioStreamingAsync(
            text,
            voiceId,
            (chunk, progress) =>
            {
                try
                {
                    // Clamp samples to [-1, 1] to prevent distortion
                    var bytes = new byte[chunk.Length * 4];
                    for (int i = 0; i < chunk.Length; i++)
                    {
                        float sample = Math.Clamp(chunk[i], -1.0f, 1.0f);
                        BitConverter.TryWriteBytes(bytes.AsSpan(i * 4), sample);
                    }
                    waveProvider.AddSamples(bytes, 0, bytes.Length);

                    // Start playback on first chunk for low latency
                    if (!playbackStarted)
                    {
                        playbackStarted = true;
                        waveOut.Play();
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError("[TTS] Error adding samples to playback buffer: {0}", ex);
                }
            },
            speed,
            cancellationToken);

        await generationTask;

        // Wait for playback to drain the buffer
        if (waveOut.PlaybackState == PlaybackState.Playing)
        {
            while (waveProvider.BufferedBytes > 0 && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(50, cancellationToken);
            }
        }

        waveOut.Stop();

        // Give PlaybackStopped a moment to fire
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            await playbackComplete.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout waiting for PlaybackStopped is fine -- audio already played
        }
    }

    /// <summary>
    /// Creates a generation config with voice reference audio loaded from the voice's wav file.
    /// </summary>
    private OfflineTtsGenerationConfig CreateGenerationConfig(string voiceId, float speed)
    {
        var voice = FindVoice(voiceId);

        var genConfig = new OfflineTtsGenerationConfig();
        genConfig.Speed = speed;

        // Load reference audio for voice cloning using NAudio
        var (samples, sampleRate) = LoadWavSamples(voice.ReferenceAudioPath);
        genConfig.ReferenceAudio = samples;
        genConfig.ReferenceSampleRate = sampleRate;
        genConfig.Extra["max_reference_audio_len"] = MaxReferenceAudioLenSeconds;

        return genConfig;
    }

    /// <summary>
    /// Finds a voice by ID, throwing if not found.
    /// </summary>
    private TtsVoice FindVoice(string voiceId)
    {
        var voices = GetAvailableVoices();
        var voice = voices.FirstOrDefault(v => v.Id == voiceId);

        if (voice is null)
        {
            throw new ArgumentException(
                $"Voice '{voiceId}' not found. Available voices: {string.Join(", ", voices.Select(v => v.Id))}",
                nameof(voiceId));
        }

        return voice;
    }

    /// <summary>
    /// Loads a WAV file as float32 mono samples using NAudio.
    /// </summary>
    private static (float[] Samples, int SampleRate) LoadWavSamples(string wavPath)
    {
        using var reader = new AudioFileReader(wavPath);
        var sampleRate = reader.WaveFormat.SampleRate;
        var channels = reader.WaveFormat.Channels;

        // Read all samples
        var totalSamples = (int)(reader.Length / (reader.WaveFormat.BitsPerSample / 8));
        var buffer = new float[totalSamples];
        int read = reader.Read(buffer, 0, buffer.Length);

        // If stereo, mix down to mono
        if (channels > 1)
        {
            var monoSamples = new float[read / channels];
            for (int i = 0; i < monoSamples.Length; i++)
            {
                float sum = 0;
                for (int ch = 0; ch < channels; ch++)
                    sum += buffer[i * channels + ch];
                monoSamples[i] = sum / channels;
            }
            return (monoSamples, sampleRate);
        }

        if (read < buffer.Length)
            Array.Resize(ref buffer, read);

        return (buffer, sampleRate);
    }

    private static void ValidateModelFile(string path, string description)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException(
                $"TTS model file not found: {description} at {path}. " +
                "Please ensure the Pocket TTS model has been downloaded.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _tts?.Dispose();
        _tts = null;
    }
}
