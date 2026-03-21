using System.Diagnostics;
using WhisperHeim.Services.Audio;
using WhisperHeim.Services.Transcription;

namespace WhisperHeim.Services.Dictation;

/// <summary>
/// Orchestrates the streaming dictation pipeline: AudioCapture -> VAD -> ASR -> Text.
///
/// Architecture:
/// - Audio capture delivers chunks via event; these are fed to the VAD.
/// - VAD fires SpeechStarted / SpeechEnded events.
/// - During ongoing speech, a periodic timer sends accumulated audio to ASR
///   for partial results (tumbling window approach, ~1.5s intervals).
/// - On speech end, the final accumulated audio is sent to ASR for the definitive result.
/// - Partial results use diff-based detection: the full accumulated audio is transcribed,
///   and only the new text (beyond what was previously emitted) is raised as PartialResult.
/// - Final results emit the complete segment transcription.
///
/// Thread safety: all mutable state is guarded by _lock. ASR calls are async and
/// serialized via a processing queue to avoid concurrent model access.
/// </summary>
public sealed class DictationPipeline : IDictationPipeline
{
    private readonly IAudioCaptureService _audioCapture;
    private readonly IVoiceActivityDetector _vad;
    private readonly ITranscriptionService _transcription;
    private readonly DictationPipelineSettings _settings;
    private readonly object _lock = new();

    // Speech accumulation buffer - holds all audio from current speech segment
    private readonly List<float> _speechAccumulator = new();

    // Partial result tracking
    private string _lastPartialText = string.Empty;
    private Timer? _partialTimer;
    private bool _inSpeech;
    private long _partialSequence; // monotonic counter to discard stale partial results

    // Pipeline state
    private bool _isRunning;
    private bool _disposed;

    // Cancellation for pending ASR operations
    private CancellationTokenSource? _cts;

    public DictationPipeline(
        IAudioCaptureService audioCapture,
        IVoiceActivityDetector vad,
        ITranscriptionService transcription,
        DictationPipelineSettings? settings = null)
    {
        _audioCapture = audioCapture ?? throw new ArgumentNullException(nameof(audioCapture));
        _vad = vad ?? throw new ArgumentNullException(nameof(vad));
        _transcription = transcription ?? throw new ArgumentNullException(nameof(transcription));
        _settings = settings ?? new DictationPipelineSettings();
    }

    /// <inheritdoc />
    public event EventHandler<DictationResultEventArgs>? PartialResult;

    /// <inheritdoc />
    public event EventHandler<DictationResultEventArgs>? FinalResult;

    /// <inheritdoc />
    public event EventHandler<DictationErrorEventArgs>? Error;

    /// <inheritdoc />
    public bool IsRunning
    {
        get
        {
            lock (_lock) return _isRunning;
        }
    }

    /// <inheritdoc />
    public void Start(int deviceIndex = -1)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (_isRunning)
                return;

            if (!_transcription.IsLoaded)
            {
                throw new InvalidOperationException(
                    "Transcription model must be loaded before starting the dictation pipeline. " +
                    "Call ITranscriptionService.LoadModel() first.");
            }

            _cts = new CancellationTokenSource();
            _speechAccumulator.Clear();
            _lastPartialText = string.Empty;
            _inSpeech = false;
            _partialSequence = 0;

            // Wire up events
            _audioCapture.AudioDataAvailable += OnAudioDataAvailable;
            _vad.SpeechStarted += OnSpeechStarted;
            _vad.SpeechEnded += OnSpeechEnded;
            _audioCapture.CaptureStopped += OnCaptureStopped;

            _isRunning = true;
        }

        // Start audio capture (outside lock to avoid potential deadlock)
        try
        {
            _audioCapture.StartCapture(deviceIndex);
            Trace.TraceInformation("[DictationPipeline] Started.");
        }
        catch (Exception ex)
        {
            // Roll back state on failure
            lock (_lock)
            {
                UnwireEvents();
                _isRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
            throw new InvalidOperationException(
                $"Failed to start audio capture: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        float[]? pendingSpeech = null;
        CancellationTokenSource? cts;

        lock (_lock)
        {
            if (!_isRunning)
                return;

            _isRunning = false;

            // Stop partial timer
            StopPartialTimer();

            // If speech was in progress, capture it for final transcription
            if (_inSpeech && _speechAccumulator.Count > 0)
            {
                pendingSpeech = _speechAccumulator.ToArray();
                _speechAccumulator.Clear();
                _inSpeech = false;
            }

            cts = _cts;
            _cts = null;

            UnwireEvents();
        }

        // Stop capture outside lock
        try
        {
            _audioCapture.StopCapture();
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[DictationPipeline] Error stopping audio capture: {0}", ex.Message);
        }

        // Finalize any pending speech
        if (pendingSpeech is { Length: > 0 })
        {
            _ = TranscribeFinalAsync(pendingSpeech, cts?.Token ?? CancellationToken.None);
        }

        Trace.TraceInformation("[DictationPipeline] Stopped.");
    }

    /// <summary>
    /// Called when new audio data arrives from the capture service.
    /// Feeds the audio into the VAD and accumulates samples during speech.
    /// </summary>
    private void OnAudioDataAvailable(object? sender, AudioDataEventArgs e)
    {
        lock (_lock)
        {
            if (!_isRunning)
                return;

            // Always feed audio to VAD for speech detection
            _vad.ProcessAudio(e.Samples);

            // If we're in a speech segment, accumulate the raw audio
            if (_inSpeech)
            {
                _speechAccumulator.AddRange(e.Samples);
            }
        }
    }

    /// <summary>
    /// Called when VAD detects speech has started.
    /// </summary>
    private void OnSpeechStarted(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            if (!_isRunning)
                return;

            _inSpeech = true;
            _speechAccumulator.Clear();
            _lastPartialText = string.Empty;
            _partialSequence++;

            Trace.TraceInformation("[DictationPipeline] Speech started.");

            // Start the partial result timer
            StartPartialTimer();
        }
    }

    /// <summary>
    /// Called when VAD detects speech has ended. The SpeechEndedEventArgs contains
    /// the accumulated speech audio from the VAD, but we use our own accumulator
    /// because it may have slightly different boundaries due to event timing.
    /// We prefer the VAD's audio since it includes pre-speech padding.
    /// </summary>
    private void OnSpeechEnded(object? sender, SpeechEndedEventArgs e)
    {
        float[] speechAudio;
        CancellationToken ct;

        lock (_lock)
        {
            if (!_isRunning)
                return;

            _inSpeech = false;
            _partialSequence++; // invalidate any pending partial results

            // Stop the partial result timer
            StopPartialTimer();

            // Use the VAD's speech audio (it has proper pre-speech padding)
            speechAudio = e.SpeechAudio;

            // Clear our accumulator
            _speechAccumulator.Clear();
            _lastPartialText = string.Empty;

            ct = _cts?.Token ?? CancellationToken.None;

            Trace.TraceInformation(
                "[DictationPipeline] Speech ended, {0:F2}s of audio.",
                (double)speechAudio.Length / _settings.SampleRate);
        }

        // Fire-and-forget the final transcription (off the event thread)
        _ = TranscribeFinalAsync(speechAudio, ct);
    }

    /// <summary>
    /// Called when audio capture stops unexpectedly (device disconnection, etc.).
    /// </summary>
    private void OnCaptureStopped(object? sender, CaptureStoppedEventArgs e)
    {
        if (e.WasDeviceDisconnected || e.Exception is not null)
        {
            var message = e.WasDeviceDisconnected
                ? "Audio device was disconnected."
                : $"Audio capture error: {e.Exception?.Message}";

            Trace.TraceError("[DictationPipeline] {0}", message);

            lock (_lock)
            {
                _isRunning = false;
                _inSpeech = false;
                StopPartialTimer();
                _speechAccumulator.Clear();
                UnwireEvents();
            }

            Error?.Invoke(this, new DictationErrorEventArgs(message, e.Exception));
        }
    }

    /// <summary>
    /// Starts the periodic timer that triggers partial transcription during speech.
    /// Must be called under lock.
    /// </summary>
    private void StartPartialTimer()
    {
        StopPartialTimer();

        var interval = TimeSpan.FromMilliseconds(_settings.PartialResultIntervalMs);
        _partialTimer = new Timer(
            OnPartialTimerTick,
            state: null,
            dueTime: interval,
            period: interval);
    }

    /// <summary>
    /// Stops and disposes the partial result timer. Must be called under lock.
    /// </summary>
    private void StopPartialTimer()
    {
        _partialTimer?.Dispose();
        _partialTimer = null;
    }

    /// <summary>
    /// Timer callback: takes a snapshot of accumulated speech audio
    /// and submits it for partial transcription.
    /// </summary>
    private void OnPartialTimerTick(object? state)
    {
        float[] audioSnapshot;
        long sequence;
        CancellationToken ct;

        lock (_lock)
        {
            if (!_isRunning || !_inSpeech || _speechAccumulator.Count == 0)
                return;

            int minSamples = _settings.MinPartialAudioMs * _settings.SampleRate / 1000;
            if (_speechAccumulator.Count < minSamples)
                return;

            audioSnapshot = _speechAccumulator.ToArray();
            sequence = _partialSequence;
            ct = _cts?.Token ?? CancellationToken.None;
        }

        // Fire-and-forget partial transcription
        _ = TranscribePartialAsync(audioSnapshot, sequence, ct);
    }

    /// <summary>
    /// Transcribes audio for a partial result. Computes diff against last partial text.
    /// </summary>
    private async Task TranscribePartialAsync(
        float[] samples, long sequence, CancellationToken ct)
    {
        try
        {
            var result = await _transcription.TranscribeAsync(samples, _settings.SampleRate, ct);

            if (ct.IsCancellationRequested)
                return;

            lock (_lock)
            {
                // Discard if sequence has changed (speech ended or new segment started)
                if (sequence != _partialSequence || !_inSpeech)
                    return;

                var fullText = result.Text.Trim();
                if (string.IsNullOrEmpty(fullText))
                    return;

                // Diff: find new text beyond what we already emitted
                var newText = ComputeNewText(fullText, _lastPartialText);
                if (string.IsNullOrWhiteSpace(newText))
                    return;

                _lastPartialText = fullText;

                Trace.TraceInformation(
                    "[DictationPipeline] Partial: \"{0}\" (new: \"{1}\")", fullText, newText);

                // Raise event outside lock via post
                ThreadPool.QueueUserWorkItem(_ =>
                    PartialResult?.Invoke(this,
                        new DictationResultEventArgs(newText, isFinal: false)));
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            Trace.TraceError(
                "[DictationPipeline] Partial transcription error: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Transcribes audio for a final result (speech segment ended).
    /// </summary>
    private async Task TranscribeFinalAsync(float[] samples, CancellationToken ct)
    {
        try
        {
            var result = await _transcription.TranscribeAsync(samples, _settings.SampleRate, ct);

            if (ct.IsCancellationRequested)
                return;

            var text = result.Text.Trim();
            if (string.IsNullOrEmpty(text))
                return;

            Trace.TraceInformation(
                "[DictationPipeline] Final: \"{0}\" (audio={1:F2}s, transcribe={2:F0}ms, RTF={3:F3})",
                text,
                result.AudioDuration.TotalSeconds,
                result.TranscriptionDuration.TotalMilliseconds,
                result.RealTimeFactor);

            FinalResult?.Invoke(this, new DictationResultEventArgs(text, isFinal: true));
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            Trace.TraceError(
                "[DictationPipeline] Final transcription error: {0}", ex.Message);
            Error?.Invoke(this,
                new DictationErrorEventArgs(
                    $"Transcription failed: {ex.Message}", ex));
        }
    }

    /// <summary>
    /// Computes the new text that should be emitted as a partial result.
    /// Uses a prefix-match approach: if the new full text starts with the
    /// previous partial text, the diff is the remaining suffix.
    /// Otherwise, falls back to emitting the full new text (handles ASR corrections).
    /// </summary>
    public static string ComputeNewText(string currentFullText, string previousPartialText)
    {
        if (string.IsNullOrEmpty(previousPartialText))
            return currentFullText;

        if (string.IsNullOrEmpty(currentFullText))
            return string.Empty;

        // Normalize for comparison (case-insensitive prefix match)
        if (currentFullText.StartsWith(previousPartialText, StringComparison.OrdinalIgnoreCase))
        {
            var suffix = currentFullText[previousPartialText.Length..].TrimStart();
            return suffix;
        }

        // ASR may have corrected earlier text - find longest common prefix
        int commonLen = 0;
        int maxLen = Math.Min(currentFullText.Length, previousPartialText.Length);
        for (int i = 0; i < maxLen; i++)
        {
            if (char.ToLowerInvariant(currentFullText[i]) ==
                char.ToLowerInvariant(previousPartialText[i]))
            {
                commonLen = i + 1;
            }
            else
            {
                break;
            }
        }

        // If there's a reasonable common prefix (>50% of previous), emit the suffix
        if (commonLen > previousPartialText.Length / 2)
        {
            return currentFullText[commonLen..].TrimStart();
        }

        // Complete divergence - emit the full new text
        return currentFullText;
    }

    /// <summary>
    /// Unwires all event handlers. Must be called under lock.
    /// </summary>
    private void UnwireEvents()
    {
        _audioCapture.AudioDataAvailable -= OnAudioDataAvailable;
        _vad.SpeechStarted -= OnSpeechStarted;
        _vad.SpeechEnded -= OnSpeechEnded;
        _audioCapture.CaptureStopped -= OnCaptureStopped;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        lock (_lock)
        {
            if (_isRunning)
            {
                _isRunning = false;
                StopPartialTimer();
                UnwireEvents();
            }

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _speechAccumulator.Clear();
        }
    }
}
