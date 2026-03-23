using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using NAudio.Wave;
using SherpaOnnx;
using WhisperHeim.Services.Models;
using WhisperHeim.Services.Settings;

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

    /// <summary>
    /// Duration of silence to insert between generated chunks (at 500-char sentence breaks).
    /// 300ms at 24kHz = 7200 silence samples. Provides a natural breathing pause.
    /// </summary>
    private const int InterChunkSilenceSamples = 7200; // 300ms

    private OfflineTts? _tts;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// In-memory cache of loaded WAV samples keyed by voice ID.
    /// Populated during warm-up and reused in <see cref="CreateGenerationConfig"/>
    /// to avoid disk I/O on each generation call.
    /// </summary>
    private readonly Dictionary<string, (float[] Samples, int SampleRate)> _wavSampleCache = new();

    /// <summary>
    /// Directory for custom voice reference .wav files (synced).
    /// Initialized from DataPathService; falls back to %APPDATA%\WhisperHeim\voices.
    /// </summary>
    private static string CustomVoicesDir =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WhisperHeim",
            "voices");

    /// <summary>
    /// Initializes the custom voices directory from the data path service.
    /// </summary>
    public static void Initialize(DataPathService dataPathService)
    {
        CustomVoicesDir = dataPathService.VoicesPath;
    }

    /// <inheritdoc />
    public bool IsLoaded => _tts is not null;

    /// <inheritdoc />
    public void UnloadModel()
    {
        lock (_lock)
        {
            if (_tts is not null)
            {
                _tts.Dispose();
                _tts = null;
                _wavSampleCache.Clear();
                Trace.TraceInformation("[TTS] Model unloaded to free native memory.");
            }
        }
    }

    /// <inheritdoc />
    public void LoadModel()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_tts is not null)
            return;

        var activeModel = ModelManagerService.ActivePocketTtsModel;
        var modelDir = ModelManagerService.GetModelDirectory(activeModel);
        var isFp32 = activeModel == ModelManagerService.PocketTtsFp32;

        var lmFlowPath = Path.Combine(modelDir, isFp32 ? "lm_flow.onnx" : "lm_flow.int8.onnx");
        var lmMainPath = Path.Combine(modelDir, isFp32 ? "lm_main.onnx" : "lm_main.int8.onnx");
        var encoderPath = Path.Combine(modelDir, "encoder.onnx");
        var decoderPath = Path.Combine(modelDir, isFp32 ? "decoder.onnx" : "decoder.int8.onnx");
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

        config.Model.Pocket.VoiceEmbeddingCacheCapacity = 10;
        config.Model.NumThreads = Math.Max(1, Environment.ProcessorCount / 2);
        config.Model.Debug = 0;
        config.Model.Provider = "cpu";

        var variant = isFp32 ? "FP32" : "int8";
        Trace.TraceInformation("[TTS] Loading Pocket TTS model ({0}) from {1}...", variant, modelDir);
        _tts = new OfflineTts(config);
        Trace.TraceInformation("[TTS] Pocket TTS model ({0}) loaded. SampleRate={1}", variant, _tts.SampleRate);
    }

    /// <inheritdoc />
    public IReadOnlyList<TtsVoice> GetAvailableVoices()
    {
        var voices = new List<TtsVoice>();

        // Built-in voices from the model's test_wavs directory
        var modelDir = ModelManagerService.GetModelDirectory(ModelManagerService.ActivePocketTtsModel);
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

            var chunks = SplitTextIntoChunks(text);
            var genConfig = CreateGenerationConfig(voiceId, speed);
            var sw = Stopwatch.StartNew();

            OfflineTtsCallbackProgressWithArg callback = (IntPtr samples, int n, float progress, IntPtr arg) =>
            {
                return cancellationToken.IsCancellationRequested ? 0 : 1;
            };

            var allSamples = new List<float>();

            for (int i = 0; i < chunks.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Insert silence between chunks for a natural pause
                if (i > 0)
                    allSamples.AddRange(new float[InterChunkSilenceSamples]);

                var audio = _tts.GenerateWithConfig(chunks[i], genConfig, callback);
                GC.KeepAlive(callback);
                allSamples.AddRange(audio.Samples);
            }

            sw.Stop();

            Trace.TraceInformation(
                "[TTS] Generated {0} samples ({1:F1}s audio) from {2} chunk(s) in {3:F1}s",
                allSamples.Count,
                (double)allSamples.Count / PocketTtsSampleRate,
                chunks.Count,
                sw.Elapsed.TotalSeconds);

            return new TtsGenerationResult(allSamples.ToArray(), PocketTtsSampleRate);
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

            var chunks = SplitTextIntoChunks(text);
            var genConfig = CreateGenerationConfig(voiceId, speed);

            for (int i = 0; i < chunks.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Insert silence between chunks for a natural pause
                if (i > 0)
                    onChunk(new float[InterChunkSilenceSamples], (float)i / chunks.Count);

                // IMPORTANT: The delegate must be prevented from garbage collection while
                // native code holds a reference to it. Store in a local and use GC.KeepAlive
                // after the native call completes.
                OfflineTtsCallbackProgressWithArg callback = (IntPtr samples, int n, float progress, IntPtr arg) =>
                {
                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return 0;

                        if (n > 0)
                        {
                            var chunk = new float[n];
                            Marshal.Copy(samples, chunk, 0, n);
                            // Scale progress to account for multiple chunks
                            float overallProgress = ((float)i + progress) / chunks.Count;
                            onChunk(chunk, overallProgress);
                        }

                        return 1;
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("[TTS] Error in streaming callback: {0}", ex);
                        return 0;
                    }
                };

                _tts.GenerateWithConfig(chunks[i], genConfig, callback);
                GC.KeepAlive(callback);
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SpeakAsync(
        string text,
        string voiceId,
        float speed = 1.0f,
        int playbackDeviceNumber = -1,
        CancellationToken cancellationToken = default,
        Action? onPlaybackStarted = null)
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
                        onPlaybackStarted?.Invoke();
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

        // More diffusion steps = smoother audio, fewer artifacts (default 5, max ~10)
        genConfig.NumSteps = 8;

        // Expand existing silent regions for more breathing room between sentences
        genConfig.SilenceScale = 0.8f;

        // Load reference audio for voice cloning, using the in-memory cache when available
        var (samples, sampleRate) = GetOrLoadWavSamples(voiceId, voice.ReferenceAudioPath);
        genConfig.ReferenceAudio = samples;
        genConfig.ReferenceSampleRate = sampleRate;
        genConfig.Extra["max_reference_audio_len"] = MaxReferenceAudioLenSeconds;

        // Lower temperature for more stable, less glitchy output (default 0.7)
        genConfig.Extra["temperature"] = 0.6f;

        // More frames after end-of-speech = natural trailing silence per sentence (default 3)
        genConfig.Extra["frames_after_eos"] = 12;

        return genConfig;
    }

    /// <inheritdoc />
    public async Task WarmUpAsync(string? defaultVoiceId)
    {
        if (string.IsNullOrWhiteSpace(defaultVoiceId))
        {
            Trace.TraceInformation("[TTS] No default voice configured, skipping warm-up.");
            return;
        }

        await Task.Run(() =>
        {
            try
            {
                // Ensure model is loaded
                LoadModel();

                // Pre-load WAV samples into memory cache
                var voice = FindVoice(defaultVoiceId);
                GetOrLoadWavSamples(defaultVoiceId, voice.ReferenceAudioPath);

                // Run a short dummy generation to populate the sherpa-onnx voice embedding cache
                var genConfig = CreateGenerationConfig(defaultVoiceId, 1.0f);
                var sw = Stopwatch.StartNew();

                OfflineTtsCallbackProgressWithArg noOpCallback =
                    (IntPtr samples, int n, float progress, IntPtr arg) => 1;
                _tts!.GenerateWithConfig("ready", genConfig, noOpCallback);
                GC.KeepAlive(noOpCallback);
                sw.Stop();

                Trace.TraceInformation(
                    "[TTS] Voice warm-up complete for '{0}' in {1:F1}s (embedding now cached).",
                    defaultVoiceId, sw.Elapsed.TotalSeconds);
            }
            catch (Exception ex)
            {
                // Warm-up failure is non-fatal -- the voice will still work on first use
                Trace.TraceWarning("[TTS] Voice warm-up failed (non-fatal): {0}", ex.Message);
            }
        });
    }

    /// <summary>
    /// Returns cached WAV samples for a voice, loading from disk on first access.
    /// </summary>
    private (float[] Samples, int SampleRate) GetOrLoadWavSamples(string voiceId, string wavPath)
    {
        lock (_wavSampleCache)
        {
            if (_wavSampleCache.TryGetValue(voiceId, out var cached))
                return cached;
        }

        var result = LoadWavSamples(wavPath);

        lock (_wavSampleCache)
        {
            _wavSampleCache[voiceId] = result;
        }

        return result;
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

    /// <summary>
    /// Max characters per chunk before forcing a split.
    /// The native code has a max_frames limit that can cut off very long text.
    /// </summary>
    private const int MaxCharsPerChunk = 500;

    /// <summary>
    /// Splits text into generation chunks. Within each chunk, sentence-ending punctuation
    /// is replaced with commas so the native code generates one continuous utterance.
    /// Chunks are split at ~500 character boundaries at natural sentence breaks.
    /// The caller is responsible for inserting silence between chunks.
    /// </summary>
    private static List<string> SplitTextIntoChunks(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [text];

        // Find all mid-text sentence boundaries: .!? followed by whitespace and uppercase letter
        var boundaries = Regex.Matches(text, @"[.!?]\s+(?=[A-Z])");
        if (boundaries.Count == 0)
            return [text];

        // First pass: decide which boundaries become chunk splits vs comma replacements
        var chunkTexts = new List<string>();
        var currentChunk = new System.Text.StringBuilder();
        int lastCopyPos = 0;
        int charsSinceLastSplit = 0;

        foreach (Match match in boundaries)
        {
            int textBeforeLen = match.Index - lastCopyPos;
            charsSinceLastSplit += textBeforeLen + match.Length;

            currentChunk.Append(text, lastCopyPos, textBeforeLen);

            if (charsSinceLastSplit >= MaxCharsPerChunk)
            {
                // End the current chunk here (keep the sentence-ending punctuation)
                currentChunk.Append(text[match.Index]); // just the . or ! or ?
                chunkTexts.Add(currentChunk.ToString().Trim());
                currentChunk.Clear();
                charsSinceLastSplit = 0;
            }
            else
            {
                // Merge: replace punctuation with comma, lowercase the next letter
                currentChunk.Append(", ");
                int nextCharIndex = match.Index + match.Length;
                currentChunk.Append(char.ToLower(text[nextCharIndex]));
                lastCopyPos = nextCharIndex + 1;
                continue;
            }

            lastCopyPos = match.Index + match.Length;
        }

        // Append remaining text as the final chunk
        if (lastCopyPos < text.Length)
            currentChunk.Append(text, lastCopyPos, text.Length - lastCopyPos);

        var finalChunk = currentChunk.ToString().Trim();
        if (finalChunk.Length > 0)
            chunkTexts.Add(finalChunk);

        return chunkTexts;
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
