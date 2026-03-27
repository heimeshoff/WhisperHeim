using System.Diagnostics;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace WhisperHeim.Services.Audio;

/// <summary>
/// Plays back audio from WAV files with seeking support for transcript segment playback.
/// Supports opening two WAV files (mic + system) and mixing them in real-time,
/// or a single WAV file for backward compatibility.
/// Uses NAudio WaveOutEvent for playback. Provides current position tracking and
/// play/pause/stop/seek controls.
/// </summary>
public sealed class TranscriptAudioPlayer : IDisposable
{
    private WaveOutEvent? _waveOut;
    private AudioFileReader? _audioReader;
    private AudioFileReader? _audioReader2;
    private string? _currentFilePath;
    private readonly object _lock = new();
    private System.Timers.Timer? _positionTimer;

    /// <summary>Raised when the playback position changes (approximately every 100ms).</summary>
    public event EventHandler<TimeSpan>? PositionChanged;

    /// <summary>Raised when playback stops (either naturally at end-of-file or by user action).</summary>
    public event EventHandler? PlaybackStopped;

    /// <summary>Whether audio is currently playing.</summary>
    public bool IsPlaying
    {
        get
        {
            lock (_lock)
                return _waveOut?.PlaybackState == PlaybackState.Playing;
        }
    }

    /// <summary>Whether audio is paused.</summary>
    public bool IsPaused
    {
        get
        {
            lock (_lock)
                return _waveOut?.PlaybackState == PlaybackState.Paused;
        }
    }

    /// <summary>Whether an audio file is loaded (playing, paused, or stopped).</summary>
    public bool IsLoaded
    {
        get
        {
            lock (_lock)
                return _audioReader is not null;
        }
    }

    /// <summary>Current playback position.</summary>
    public TimeSpan CurrentPosition
    {
        get
        {
            lock (_lock)
                return _audioReader?.CurrentTime ?? TimeSpan.Zero;
        }
    }

    /// <summary>Total duration of the loaded audio file.</summary>
    public TimeSpan TotalDuration
    {
        get
        {
            lock (_lock)
                return _audioReader?.TotalTime ?? TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Opens an audio file for playback. If a file is already loaded, it is closed first.
    /// </summary>
    /// <param name="filePath">Absolute path to the WAV file.</param>
    public void Open(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Audio file not found.", filePath);

        lock (_lock)
        {
            CloseInternal();

            _audioReader = new AudioFileReader(filePath);
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_audioReader);
            _waveOut.PlaybackStopped += OnPlaybackStopped;
            _currentFilePath = filePath;

            StartPositionTimer();

            Trace.TraceInformation(
                "[TranscriptAudioPlayer] Opened: {0} (duration={1:hh\\:mm\\:ss})",
                filePath, _audioReader.TotalTime);
        }
    }

    /// <summary>
    /// Opens two WAV files (mic + system) and mixes them in real-time for playback.
    /// If only one file exists, falls back to single-file playback.
    /// </summary>
    public void Open(string micFilePath, string systemFilePath)
    {
        var micExists = File.Exists(micFilePath);
        var sysExists = File.Exists(systemFilePath);

        if (!micExists && !sysExists)
            throw new FileNotFoundException("No audio files found.");

        if (!micExists || !sysExists)
        {
            Open(micExists ? micFilePath : systemFilePath);
            return;
        }

        lock (_lock)
        {
            CloseInternal();

            _audioReader = new AudioFileReader(micFilePath);
            _audioReader2 = new AudioFileReader(systemFilePath);

            var micSamples = _audioReader.ToSampleProvider();
            var sysSamples = _audioReader2.ToSampleProvider();

            // Resample system audio to match mic sample rate if different
            ISampleProvider sysProvider = sysSamples;
            if (_audioReader2.WaveFormat.SampleRate != _audioReader.WaveFormat.SampleRate)
            {
                sysProvider = new WdlResamplingSampleProvider(
                    sysSamples, _audioReader.WaveFormat.SampleRate);
            }

            // Ensure both are mono
            ISampleProvider micMono = _audioReader.WaveFormat.Channels > 1
                ? new StereoToMonoSampleProvider(micSamples) : micSamples;
            ISampleProvider sysMono = _audioReader2.WaveFormat.Channels > 1
                ? new StereoToMonoSampleProvider(sysProvider) : sysProvider;

            var mixer = new MixingSampleProvider(new[] { micMono, sysMono })
            {
                ReadFully = false
            };

            _waveOut = new WaveOutEvent();
            _waveOut.Init(mixer);
            _waveOut.PlaybackStopped += OnPlaybackStopped;
            _currentFilePath = micFilePath;

            StartPositionTimer();

            Trace.TraceInformation(
                "[TranscriptAudioPlayer] Opened mixed: {0} + {1} (duration={2:hh\\:mm\\:ss})",
                micFilePath, systemFilePath, _audioReader.TotalTime);
        }
    }

    /// <summary>
    /// Starts or resumes playback from the current position.
    /// </summary>
    public void Play()
    {
        lock (_lock)
        {
            if (_waveOut is null || _audioReader is null)
                return;

            _waveOut.Play();
        }
    }

    /// <summary>
    /// Starts playback from a specific position (e.g., a transcript segment's StartTime).
    /// </summary>
    /// <param name="position">The position to seek to before playing.</param>
    public void PlayFrom(TimeSpan position)
    {
        lock (_lock)
        {
            if (_waveOut is null || _audioReader is null)
                return;

            _audioReader.CurrentTime = position;
            if (_audioReader2 is not null)
                _audioReader2.CurrentTime = position;
            _waveOut.Play();
        }
    }

    /// <summary>Pauses playback.</summary>
    public void Pause()
    {
        lock (_lock)
            _waveOut?.Pause();
    }

    /// <summary>Stops playback and resets position to the beginning.</summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (_waveOut is not null)
            {
                _waveOut.Stop();
                if (_audioReader is not null)
                    _audioReader.CurrentTime = TimeSpan.Zero;
                if (_audioReader2 is not null)
                    _audioReader2.CurrentTime = TimeSpan.Zero;
            }
        }
    }

    /// <summary>Seeks to a specific position without changing play/pause state.</summary>
    /// <param name="position">The position to seek to.</param>
    public void Seek(TimeSpan position)
    {
        lock (_lock)
        {
            if (_audioReader is not null)
                _audioReader.CurrentTime = position;
            if (_audioReader2 is not null)
                _audioReader2.CurrentTime = position;
        }
    }

    /// <summary>Toggles between play and pause.</summary>
    public void TogglePlayPause()
    {
        lock (_lock)
        {
            if (_waveOut is null)
                return;

            if (_waveOut.PlaybackState == PlaybackState.Playing)
                _waveOut.Pause();
            else
                _waveOut.Play();
        }
    }

    /// <summary>Closes the current audio file and releases resources.</summary>
    public void Close()
    {
        lock (_lock)
            CloseInternal();
    }

    public void Dispose()
    {
        lock (_lock)
            CloseInternal();
    }

    private void CloseInternal()
    {
        StopPositionTimer();

        if (_waveOut is not null)
        {
            _waveOut.PlaybackStopped -= OnPlaybackStopped;
            _waveOut.Stop();
            _waveOut.Dispose();
            _waveOut = null;
        }

        if (_audioReader is not null)
        {
            _audioReader.Dispose();
            _audioReader = null;
        }

        if (_audioReader2 is not null)
        {
            _audioReader2.Dispose();
            _audioReader2 = null;
        }

        _currentFilePath = null;
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }

    private void StartPositionTimer()
    {
        StopPositionTimer();
        _positionTimer = new System.Timers.Timer(100); // 100ms interval
        _positionTimer.Elapsed += (_, _) =>
        {
            TimeSpan pos;
            lock (_lock)
            {
                if (_audioReader is null || _waveOut?.PlaybackState != PlaybackState.Playing)
                    return;
                pos = _audioReader.CurrentTime;
            }
            PositionChanged?.Invoke(this, pos);
        };
        _positionTimer.Start();
    }

    private void StopPositionTimer()
    {
        _positionTimer?.Stop();
        _positionTimer?.Dispose();
        _positionTimer = null;
    }
}
