namespace WhisperHeim.Services.Input;

/// <summary>
/// Types text into the currently focused Windows application using Win32 SendInput.
/// </summary>
public interface IInputSimulator
{
    /// <summary>
    /// Types the given text into the focused window using synthetic keyboard events.
    /// Supports Unicode characters via KEYEVENTF_UNICODE.
    /// </summary>
    Task TypeTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the specified number of backspace key presses to the focused window.
    /// Used for correcting partial transcription results.
    /// </summary>
    Task SendBackspacesAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delay in milliseconds between individual keystrokes.
    /// Some applications need a small delay to process input reliably.
    /// </summary>
    int KeystrokeDelayMs { get; set; }
}
