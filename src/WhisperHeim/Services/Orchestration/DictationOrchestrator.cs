using System.Diagnostics;
using System.Windows;
using WhisperHeim.Services.Audio;
using WhisperHeim.Services.Hotkey;
using WhisperHeim.Services.Input;
using WhisperHeim.Services.Transcription;

namespace WhisperHeim.Services.Orchestration;

/// <summary>
/// Hold-to-talk dictation orchestrator.
///
/// Key down: start recording + show overlay.
/// While holding: periodically transcribe accumulated audio for partial results.
/// Key up: stop recording, transcribe full audio, type the final result.
///
/// No VAD needed -- the user controls speech boundaries by holding/releasing the hotkey.
/// </summary>
public sealed class DictationOrchestrator : IDisposable
{
    private readonly GlobalHotkeyService _hotkeyService;
    private readonly IAudioCaptureService _audioCapture;
    private readonly ITranscriptionService _transcription;
    private readonly IInputSimulator _inputSimulator;
    private readonly Action<bool> _onDictationStateChanged;

    private readonly object _lock = new();
    private readonly List<float> _recordedSamples = new();
    private bool _isRecording;
    private bool _disposed;

    private const int MinSamples = 8000; // 0.5s at 16kHz
    private const int SampleRate = 16000;

    public DictationOrchestrator(
        GlobalHotkeyService hotkeyService,
        IAudioCaptureService audioCapture,
        ITranscriptionService transcription,
        IInputSimulator inputSimulator,
        Action<bool> onDictationStateChanged)
    {
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
        _audioCapture = audioCapture ?? throw new ArgumentNullException(nameof(audioCapture));
        _transcription = transcription ?? throw new ArgumentNullException(nameof(transcription));
        _inputSimulator = inputSimulator ?? throw new ArgumentNullException(nameof(inputSimulator));
        _onDictationStateChanged = onDictationStateChanged ?? throw new ArgumentNullException(nameof(onDictationStateChanged));
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        _hotkeyService.HotkeyReleased += OnHotkeyReleased;

        Trace.TraceInformation("[DictationOrchestrator] Started. Hold hotkey to dictate.");
    }

    public void Stop()
    {
        _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
        _hotkeyService.HotkeyReleased -= OnHotkeyReleased;

        if (_isRecording)
            StopRecording(transcribe: false);

        Trace.TraceInformation("[DictationOrchestrator] Stopped.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            if (_isRecording) return; // already recording
            _isRecording = true;
        }

        Trace.TraceInformation("[DictationOrchestrator] Hotkey pressed -- starting recording.");

        _recordedSamples.Clear();

        _audioCapture.AudioDataAvailable += OnAudioData;

        try
        {
            _audioCapture.StartCapture();
        }
        catch (Exception ex)
        {
            Trace.TraceError("[DictationOrchestrator] Failed to start capture: {0}", ex.Message);
            _audioCapture.AudioDataAvailable -= OnAudioData;
            lock (_lock) _isRecording = false;
            return;
        }

        NotifyStateChanged(true);
    }

    private void OnHotkeyReleased(object? sender, EventArgs e)
    {
        bool wasRecording;
        lock (_lock)
        {
            wasRecording = _isRecording;
            if (!wasRecording) return;
        }

        Trace.TraceInformation("[DictationOrchestrator] Hotkey released -- stopping and transcribing.");
        StopRecording(transcribe: true);
    }

    private void StopRecording(bool transcribe)
    {
        lock (_lock)
        {
            if (!_isRecording) return;
            _isRecording = false;
        }

        // Stop capture
        _audioCapture.AudioDataAvailable -= OnAudioData;
        try { _audioCapture.StopCapture(); }
        catch (Exception ex) { Trace.TraceWarning("[DictationOrchestrator] Error stopping capture: {0}", ex.Message); }

        NotifyStateChanged(false);

        if (transcribe)
        {
            float[] samples;
            lock (_lock)
            {
                samples = _recordedSamples.ToArray();
                _recordedSamples.Clear();
            }

            if (samples.Length > MinSamples)
            {
                _ = TranscribeFinalAsync(samples);
            }
            else
            {
                Trace.TraceInformation("[DictationOrchestrator] Recording too short ({0} samples), skipping.", samples.Length);
            }
        }
    }

    private void OnAudioData(object? sender, AudioDataEventArgs e)
    {
        lock (_lock)
        {
            if (_isRecording)
                _recordedSamples.AddRange(e.Samples);
        }
    }

    private async Task TranscribeFinalAsync(float[] samples)
    {
        try
        {
            var result = await _transcription.TranscribeAsync(samples, SampleRate);
            var text = result.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            Trace.TraceInformation(
                "[DictationOrchestrator] Final: \"{0}\" (audio={1:F2}s, transcribe={2:F0}ms, RTF={3:F3})",
                text, result.AudioDuration.TotalSeconds,
                result.TranscriptionDuration.TotalMilliseconds, result.RealTimeFactor);

            await TypeTextSafe(text);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[DictationOrchestrator] Final transcription error: {0}", ex.Message);
        }
    }

    private async Task TypeTextSafe(string text)
    {
        try
        {
            await _inputSimulator.TypeTextAsync(text);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[DictationOrchestrator] Failed to type text: {0}", ex.Message);
        }
    }

    private void NotifyStateChanged(bool isActive)
    {
        try
        {
            Application.Current?.Dispatcher?.BeginInvoke(() =>
            {
                try { _onDictationStateChanged(isActive); }
                catch (Exception ex) { Trace.TraceError("[DictationOrchestrator] State callback error: {0}", ex.Message); }
            });
        }
        catch (Exception ex)
        {
            Trace.TraceError("[DictationOrchestrator] Failed to dispatch state change: {0}", ex.Message);
        }
    }
}
