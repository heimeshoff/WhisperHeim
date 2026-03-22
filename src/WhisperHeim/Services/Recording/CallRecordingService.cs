using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using NAudio.Wave;
using WhisperHeim.Services.Audio;
using WhisperHeim.Services.Settings;

namespace WhisperHeim.Services.Recording;

/// <summary>
/// Orchestrates simultaneous microphone (AudioCaptureService) and system audio
/// (LoopbackCaptureService) capture for call recording. Each stream is saved to
/// WAV files in the recording session directory. If one stream fails,
/// the other continues recording independently.
/// </summary>
public sealed class CallRecordingService : ICallRecordingService
{
    private const int TargetSampleRate = 16000;
    private const int TargetChannels = 1;

    private readonly object _lock = new();
    private readonly DispatcherTimer _durationTimer;
    private readonly DataPathService? _dataPathService;

    private AudioCaptureService? _micCapture;
    private LoopbackCaptureService? _loopbackCapture;
    private WaveFileWriter? _micWaveWriter;
    private string? _micWavFilePath;

    private CallRecordingSession? _currentSession;
    private bool _micStreamActive;
    private bool _systemStreamActive;
    private bool _disposed;

    public CallRecordingService(DataPathService? dataPathService = null)
    {
        _dataPathService = dataPathService;
        _durationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _durationTimer.Tick += OnDurationTimerTick;
    }

    /// <inheritdoc />
    public event EventHandler<CallRecordingSession>? RecordingStarted;

    /// <inheritdoc />
    public event EventHandler<CallRecordingStoppedEventArgs>? RecordingStopped;

    /// <inheritdoc />
    public event EventHandler<StreamFailedEventArgs>? StreamFailed;

    /// <summary>
    /// Raised every second while recording, carrying the current duration.
    /// Useful for updating tray tooltip or overlay display.
    /// </summary>
    public event EventHandler<TimeSpan>? DurationUpdated;

    /// <inheritdoc />
    public bool IsRecording { get; private set; }

    /// <inheritdoc />
    public CallRecordingSession? CurrentSession => _currentSession;

    /// <inheritdoc />
    public void StartRecording(int micDeviceIndex = -1)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (IsRecording)
                return;

            var startTimestamp = DateTimeOffset.UtcNow;
            var sessionId = $"{startTimestamp:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";

            // Create WAV file paths — use the data path recordings directory if available,
            // otherwise fall back to %TEMP%
            string recordingDir;
            if (_dataPathService is not null)
            {
                var sessionName = startTimestamp.LocalDateTime.ToString("yyyyMMdd_HHmmss");
                recordingDir = Path.Combine(_dataPathService.RecordingsPath, sessionName);
                // Avoid collision
                if (Directory.Exists(recordingDir))
                {
                    var suffix = 1;
                    string candidate;
                    do
                    {
                        candidate = $"{recordingDir}_{suffix}";
                        suffix++;
                    } while (Directory.Exists(candidate));
                    recordingDir = candidate;
                }
            }
            else
            {
                recordingDir = Path.Combine(Path.GetTempPath(), "WhisperHeim");
            }
            Directory.CreateDirectory(recordingDir);

            _micWavFilePath = Path.Combine(recordingDir, "mic.wav");
            var systemWavFilePath = Path.Combine(recordingDir, "system.wav");

            _currentSession = new CallRecordingSession(
                _micWavFilePath, systemWavFilePath, startTimestamp);

            // Start mic capture with WAV file writer
            _micCapture = new AudioCaptureService();
            var micFormat = WaveFormat.CreateIeeeFloatWaveFormat(TargetSampleRate, TargetChannels);
            _micWaveWriter = new WaveFileWriter(_micWavFilePath, micFormat);

            _micCapture.AudioDataAvailable += OnMicDataAvailable;
            _micCapture.CaptureStopped += OnMicCaptureStopped;

            // Start loopback capture (it writes its own WAV via TempWavFilePath)
            _loopbackCapture = new LoopbackCaptureService();
            _loopbackCapture.CaptureStopped += OnLoopbackCaptureStopped;

            // Start both streams -- if one fails, continue with the other
            _micStreamActive = false;
            _systemStreamActive = false;
            Exception? micError = null;
            Exception? systemError = null;

            try
            {
                _micCapture.StartCapture(micDeviceIndex);
                _micStreamActive = true;
            }
            catch (Exception ex)
            {
                micError = ex;
                Debug.WriteLine($"[CallRecordingService] Mic capture failed to start: {ex.Message}");
            }

            try
            {
                _loopbackCapture.StartCapture();
                _systemStreamActive = true;

                // The LoopbackCaptureService writes to its own temp file;
                // update the session's system path to match the actual path used
                if (_loopbackCapture.TempWavFilePath is not null &&
                    _loopbackCapture.TempWavFilePath != systemWavFilePath)
                {
                    // LoopbackCaptureService generates its own file path,
                    // so we update our session to reference it.
                    _currentSession = new CallRecordingSession(
                        _micWavFilePath,
                        _loopbackCapture.TempWavFilePath,
                        startTimestamp);
                }
            }
            catch (Exception ex)
            {
                systemError = ex;
                Debug.WriteLine($"[CallRecordingService] System capture failed to start: {ex.Message}");
            }

            // If both failed, clean up and throw
            if (!_micStreamActive && !_systemStreamActive)
            {
                CleanupMic();
                CleanupLoopback();
                throw new InvalidOperationException(
                    "Failed to start both microphone and system audio capture.",
                    micError ?? systemError);
            }

            IsRecording = true;
            _durationTimer.Start();

            // Notify about partial failures
            if (micError is not null)
            {
                StreamFailed?.Invoke(this, new StreamFailedEventArgs(AudioStreamKind.Microphone, micError));
            }

            if (systemError is not null)
            {
                StreamFailed?.Invoke(this, new StreamFailedEventArgs(AudioStreamKind.System, systemError));
            }

            RecordingStarted?.Invoke(this, _currentSession);
        }
    }

    /// <inheritdoc />
    public void StopRecording()
    {
        lock (_lock)
        {
            if (!IsRecording)
                return;

            IsRecording = false;
            _durationTimer.Stop();

            // Stop both streams
            try
            {
                _micCapture?.StopCapture();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CallRecordingService] Error stopping mic: {ex.Message}");
            }

            try
            {
                _loopbackCapture?.StopCapture();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CallRecordingService] Error stopping loopback: {ex.Message}");
            }

            FinalizeSession();
        }
    }

    /// <inheritdoc />
    public void ToggleRecording(int micDeviceIndex = -1)
    {
        if (IsRecording)
            StopRecording();
        else
            StartRecording(micDeviceIndex);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _durationTimer.Stop();

        if (IsRecording)
        {
            IsRecording = false;
            FinalizeSession();
        }

        CleanupMic();
        CleanupLoopback();
    }

    /// <summary>
    /// Formats a duration as HH:MM:SS or MM:SS for display purposes.
    /// </summary>
    public static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? duration.ToString(@"hh\:mm\:ss")
            : duration.ToString(@"mm\:ss");
    }

    // ── Event handlers ───────────────────────────────────────────────

    private void OnMicDataAvailable(object? sender, AudioDataEventArgs e)
    {
        try
        {
            _micWaveWriter?.WriteSamples(e.Samples, 0, e.Samples.Length);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CallRecordingService] Error writing mic samples: {ex.Message}");
        }
    }

    private void OnMicCaptureStopped(object? sender, CaptureStoppedEventArgs e)
    {
        try
        {
            lock (_lock)
            {
                bool wasActive = _micStreamActive;
                _micStreamActive = false;

                if (wasActive && IsRecording && e.WasDeviceDisconnected)
                {
                    // Mic stream failed but system may still be running
                    StreamFailed?.Invoke(this,
                        new StreamFailedEventArgs(AudioStreamKind.Microphone, e.Exception));

                    // If both streams are now dead, stop the whole recording
                    if (!_systemStreamActive)
                    {
                        IsRecording = false;
                        _durationTimer.Stop();
                        FinalizeSession();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CallRecordingService] OnMicCaptureStopped error: {ex}");
        }
    }

    private void OnLoopbackCaptureStopped(object? sender, CaptureStoppedEventArgs e)
    {
        try
        {
            lock (_lock)
            {
                bool wasActive = _systemStreamActive;
                _systemStreamActive = false;

                if (wasActive && IsRecording && e.WasDeviceDisconnected)
                {
                    // System stream failed but mic may still be running
                    StreamFailed?.Invoke(this,
                        new StreamFailedEventArgs(AudioStreamKind.System, e.Exception));

                    // If both streams are now dead, stop the whole recording
                    if (!_micStreamActive)
                    {
                        IsRecording = false;
                        _durationTimer.Stop();
                        FinalizeSession();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CallRecordingService] OnLoopbackCaptureStopped error: {ex}");
        }
    }

    private void OnDurationTimerTick(object? sender, EventArgs e)
    {
        if (_currentSession is not null)
        {
            DurationUpdated?.Invoke(this, _currentSession.Duration);
        }
    }

    // ── Cleanup ──────────────────────────────────────────────────────

    private void FinalizeSession()
    {
        if (_currentSession is not null)
        {
            _currentSession.EndTimestamp = DateTimeOffset.UtcNow;

            // Flush and close mic WAV writer
            try
            {
                _micWaveWriter?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CallRecordingService] Error disposing mic writer: {ex.Message}");
            }
            _micWaveWriter = null;

            var session = _currentSession;
            CleanupMic();
            CleanupLoopback();

            RecordingStopped?.Invoke(this,
                new CallRecordingStoppedEventArgs(session));
        }
    }

    private void CleanupMic()
    {
        if (_micCapture is not null)
        {
            _micCapture.AudioDataAvailable -= OnMicDataAvailable;
            _micCapture.CaptureStopped -= OnMicCaptureStopped;
            _micCapture.Dispose();
            _micCapture = null;
        }

        if (_micWaveWriter is not null)
        {
            try
            {
                _micWaveWriter.Dispose();
            }
            catch (Exception)
            {
                // Ignore disposal errors.
            }
            _micWaveWriter = null;
        }
    }

    private void CleanupLoopback()
    {
        if (_loopbackCapture is not null)
        {
            _loopbackCapture.CaptureStopped -= OnLoopbackCaptureStopped;
            _loopbackCapture.Dispose();
            _loopbackCapture = null;
        }
    }
}
