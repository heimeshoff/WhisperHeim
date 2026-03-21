namespace WhisperHeim.Views;

/// <summary>
/// Represents the visual state of the read-aloud overlay indicator.
/// </summary>
public enum ReadAloudOverlayState
{
    /// <summary>
    /// TTS model is loading and/or generating audio.
    /// Overlay: purple, pulsing/spinning animation.
    /// </summary>
    Thinking,

    /// <summary>
    /// Audio is actively playing back.
    /// Overlay: purple, animated sound wave.
    /// </summary>
    Playing
}
