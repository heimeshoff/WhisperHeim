using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace WhisperHeim.Services.Transcription;

/// <summary>
/// Centralized guard that tracks whether the transcription engine is currently in use.
/// The Parakeet TDT model cannot handle concurrent requests safely, so all transcription
/// entry points (file transcription, call recording pipeline, dictation) must acquire
/// the busy state before starting.
///
/// Exposes <see cref="IsBusy"/> as an observable property for UI binding (e.g. disabling
/// transcribe buttons with an "Engine busy" label while a transcription is in progress).
/// </summary>
public sealed class TranscriptionBusyService : INotifyPropertyChanged
{
    private bool _isBusy;
    private string _busySource = string.Empty;

    /// <summary>
    /// Whether a transcription is currently in progress anywhere in the application.
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// A human-readable description of what is currently using the engine
    /// (e.g. "Call transcription", "File transcription").
    /// Empty when <see cref="IsBusy"/> is false.
    /// </summary>
    public string BusySource
    {
        get => _busySource;
        private set
        {
            if (_busySource != value)
            {
                _busySource = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Attempts to acquire the transcription engine. Returns true if the engine
    /// was free and is now reserved for the caller. Returns false if already busy.
    /// </summary>
    /// <param name="source">Description of the caller (e.g. "File transcription").</param>
    public bool TryAcquire(string source)
    {
        lock (this)
        {
            if (_isBusy)
            {
                Trace.TraceWarning(
                    "[TranscriptionBusyService] Engine busy (current: '{0}'). " +
                    "Rejected acquire from '{1}'.",
                    _busySource, source);
                return false;
            }

            BusySource = source;
            IsBusy = true;

            Trace.TraceInformation(
                "[TranscriptionBusyService] Engine acquired by '{0}'.", source);
            return true;
        }
    }

    /// <summary>
    /// Releases the transcription engine, allowing other callers to acquire it.
    /// Safe to call even if not currently busy.
    /// </summary>
    public void Release()
    {
        lock (this)
        {
            if (!_isBusy)
                return;

            Trace.TraceInformation(
                "[TranscriptionBusyService] Engine released by '{0}'.", _busySource);

            BusySource = string.Empty;
            IsBusy = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
