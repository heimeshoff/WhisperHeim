namespace WhisperHeim.Views;

/// <summary>
/// Represents the visual state of the dictation overlay microphone indicator.
/// </summary>
public enum OverlayMicState
{
    /// <summary>
    /// Microphone is connected and listening, but no speech is detected.
    /// Overlay: green, static.
    /// </summary>
    Idle,

    /// <summary>
    /// VAD has detected speech. Overlay: green with RMS-driven ring scaling.
    /// </summary>
    Speaking,

    /// <summary>
    /// No microphone found or no audio input available.
    /// Overlay: grey, static.
    /// </summary>
    NoMic,

    /// <summary>
    /// A pipeline or system error has occurred.
    /// Overlay: red, static.
    /// </summary>
    Error
}
