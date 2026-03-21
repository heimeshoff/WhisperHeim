using WhisperHeim.Services.Audio;
using WhisperHeim.Services.Dictation;
using WhisperHeim.Services.Transcription;

namespace WhisperHeim.Tests;

public class DictationPipelineTests
{
    [Fact]
    public void Start_ThrowsIfModelNotLoaded()
    {
        var audio = new FakeAudioCapture();
        var vad = new FakeVad();
        var asr = new FakeTranscription { IsLoaded = false };

        using var pipeline = new DictationPipeline(audio, vad, asr);

        Assert.Throws<InvalidOperationException>(() => pipeline.Start());
    }

    [Fact]
    public void Start_SetsIsRunning()
    {
        var audio = new FakeAudioCapture();
        var vad = new FakeVad();
        var asr = new FakeTranscription { IsLoaded = true };

        using var pipeline = new DictationPipeline(audio, vad, asr);
        pipeline.Start();

        Assert.True(pipeline.IsRunning);
    }

    [Fact]
    public void Stop_ClearsIsRunning()
    {
        var audio = new FakeAudioCapture();
        var vad = new FakeVad();
        var asr = new FakeTranscription { IsLoaded = true };

        using var pipeline = new DictationPipeline(audio, vad, asr);
        pipeline.Start();
        pipeline.Stop();

        Assert.False(pipeline.IsRunning);
    }

    [Fact]
    public void Start_CalledTwice_IsIdempotent()
    {
        var audio = new FakeAudioCapture();
        var vad = new FakeVad();
        var asr = new FakeTranscription { IsLoaded = true };

        using var pipeline = new DictationPipeline(audio, vad, asr);
        pipeline.Start();
        pipeline.Start(); // should not throw

        Assert.True(pipeline.IsRunning);
        Assert.Equal(1, audio.StartCount);
    }

    [Fact]
    public async Task SpeechEnded_ProducesFinalResult()
    {
        var audio = new FakeAudioCapture();
        var vad = new FakeVad();
        var asr = new FakeTranscription
        {
            IsLoaded = true,
            NextResult = new TranscriptionResult(
                "hello world", TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(200), 0.2)
        };

        using var pipeline = new DictationPipeline(audio, vad, asr);

        var finalResults = new List<string>();
        var resultReceived = new TaskCompletionSource<bool>();

        pipeline.FinalResult += (_, e) =>
        {
            finalResults.Add(e.Text);
            resultReceived.TrySetResult(true);
        };

        pipeline.Start();

        // Simulate speech ending via VAD
        vad.SimulateSpeechEnded(new float[16000]); // 1 second of silence

        // Wait for async transcription to complete
        var completed = await Task.WhenAny(resultReceived.Task, Task.Delay(5000));
        Assert.True(completed == resultReceived.Task, "Final result was not received within timeout.");

        Assert.Single(finalResults);
        Assert.Equal("hello world", finalResults[0]);
    }

    [Fact]
    public void AudioData_FedToVad()
    {
        var audio = new FakeAudioCapture();
        var vad = new FakeVad();
        var asr = new FakeTranscription { IsLoaded = true };

        using var pipeline = new DictationPipeline(audio, vad, asr);
        pipeline.Start();

        var samples = new float[] { 0.1f, 0.2f, 0.3f };
        audio.SimulateAudioData(samples);

        Assert.Equal(1, vad.ProcessAudioCallCount);
    }

    [Fact]
    public void ComputeNewText_EmptyPrevious_ReturnsFullText()
    {
        var result = DictationPipeline.ComputeNewText("hello world", "");
        Assert.Equal("hello world", result);
    }

    [Fact]
    public void ComputeNewText_PrefixMatch_ReturnsSuffix()
    {
        var result = DictationPipeline.ComputeNewText("hello world how are you", "hello world");
        Assert.Equal("how are you", result);
    }

    [Fact]
    public void ComputeNewText_NoPrefixMatch_ReturnsFullText()
    {
        var result = DictationPipeline.ComputeNewText("completely different", "hello world");
        Assert.Equal("completely different", result);
    }

    [Fact]
    public void ComputeNewText_PartialPrefixMatch_ReturnsSuffix()
    {
        // When ASR corrects a word but keeps most of the prefix
        var result = DictationPipeline.ComputeNewText("hello world today", "hello worlds");
        // "hello world" is common prefix (11 chars), > 50% of "hello worlds" (12 chars)
        Assert.Equal("today", result);
    }

    [Fact]
    public void Dispose_WhileRunning_DoesNotThrow()
    {
        var audio = new FakeAudioCapture();
        var vad = new FakeVad();
        var asr = new FakeTranscription { IsLoaded = true };

        var pipeline = new DictationPipeline(audio, vad, asr);
        pipeline.Start();
        pipeline.Dispose(); // should not throw
    }

    [Fact]
    public void Stop_WhenNotRunning_IsNoop()
    {
        var audio = new FakeAudioCapture();
        var vad = new FakeVad();
        var asr = new FakeTranscription { IsLoaded = true };

        using var pipeline = new DictationPipeline(audio, vad, asr);
        pipeline.Stop(); // should not throw
    }

    [Fact]
    public async Task DeviceDisconnect_RaisesError()
    {
        var audio = new FakeAudioCapture();
        var vad = new FakeVad();
        var asr = new FakeTranscription { IsLoaded = true };

        using var pipeline = new DictationPipeline(audio, vad, asr);

        var errorReceived = new TaskCompletionSource<string>();
        pipeline.Error += (_, e) => errorReceived.TrySetResult(e.Message);

        pipeline.Start();
        audio.SimulateCaptureStopped(wasDeviceDisconnected: true);

        var completed = await Task.WhenAny(errorReceived.Task, Task.Delay(2000));
        Assert.True(completed == errorReceived.Task, "Error was not received within timeout.");
        Assert.Contains("disconnected", errorReceived.Task.Result, StringComparison.OrdinalIgnoreCase);
        Assert.False(pipeline.IsRunning);
    }

    #region Fakes

    private sealed class FakeAudioCapture : IAudioCaptureService
    {
        public event EventHandler<AudioDataEventArgs>? AudioDataAvailable;
        public event EventHandler? CaptureStarted;
        public event EventHandler<CaptureStoppedEventArgs>? CaptureStopped;

        public bool IsCapturing { get; private set; }
        public int StartCount { get; private set; }

        public IReadOnlyList<AudioDeviceInfo> GetAvailableDevices() => [];

        public void StartCapture(int deviceIndex = -1)
        {
            IsCapturing = true;
            StartCount++;
            CaptureStarted?.Invoke(this, EventArgs.Empty);
        }

        public void StopCapture()
        {
            IsCapturing = false;
        }

        public void SimulateAudioData(float[] samples)
        {
            AudioDataAvailable?.Invoke(this, new AudioDataEventArgs(samples));
        }

        public void SimulateCaptureStopped(bool wasDeviceDisconnected, Exception? ex = null)
        {
            CaptureStopped?.Invoke(this,
                new CaptureStoppedEventArgs(wasDeviceDisconnected, ex));
        }

        public void Dispose() { }
    }

    private sealed class FakeVad : IVoiceActivityDetector
    {
        public event EventHandler? SpeechStarted;
        public event EventHandler<SpeechEndedEventArgs>? SpeechEnded;

        public bool IsSpeechDetected { get; set; }
        public int ProcessAudioCallCount { get; private set; }

        public void ProcessAudio(float[] samples)
        {
            ProcessAudioCallCount++;
        }

        public void Reset() { }

        public void SimulateSpeechStarted()
        {
            IsSpeechDetected = true;
            SpeechStarted?.Invoke(this, EventArgs.Empty);
        }

        public void SimulateSpeechEnded(float[] speechAudio)
        {
            IsSpeechDetected = false;
            SpeechEnded?.Invoke(this, new SpeechEndedEventArgs(speechAudio));
        }

        public void Dispose() { }
    }

    private sealed class FakeTranscription : ITranscriptionService
    {
        public bool IsLoaded { get; set; }

        public TranscriptionResult NextResult { get; set; } =
            new("", TimeSpan.Zero, TimeSpan.Zero, 0);

        public void LoadModel() => IsLoaded = true;

        public Task<TranscriptionResult> TranscribeAsync(
            float[] samples, int sampleRate = 16000,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(NextResult);
        }

        public void Dispose() { }
    }

    #endregion
}
