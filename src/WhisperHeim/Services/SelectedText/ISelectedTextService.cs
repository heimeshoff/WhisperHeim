namespace WhisperHeim.Services.SelectedText;

/// <summary>
/// Captures selected text from any Windows application using a cascading strategy:
/// first UI Automation, then simulated Ctrl+C with clipboard backup/restore.
/// </summary>
public interface ISelectedTextService
{
    /// <summary>
    /// Captures the currently selected text from the focused application.
    /// Returns null if no text is selected or capture fails.
    /// </summary>
    Task<string?> CaptureSelectedTextAsync(CancellationToken cancellationToken = default);
}
