using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using WhisperHeim.Services.TextToSpeech;

namespace WhisperHeim.Views.Pages;

/// <summary>
/// Text-to-speech page: enter text, select a voice, and play speech audio.
/// </summary>
public partial class TextToSpeechPage : UserControl
{
    private const string PreviewPhrase = "Hello, this is a voice preview.";

    private readonly ITextToSpeechService _ttsService;
    private CancellationTokenSource? _cts;
    private bool _isSpeaking;

    public TextToSpeechPage(ITextToSpeechService ttsService)
    {
        _ttsService = ttsService;

        InitializeComponent();

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PopulateVoices();
    }

    private void PopulateVoices()
    {
        VoiceCombo.Items.Clear();

        try
        {
            // Ensure the model is loaded so voices are available
            if (!_ttsService.IsLoaded)
            {
                _ttsService.LoadModel();
            }

            var voices = _ttsService.GetAvailableVoices();
            foreach (var voice in voices)
            {
                var label = voice.IsBuiltIn
                    ? voice.DisplayName
                    : $"{voice.DisplayName} (custom)";
                VoiceCombo.Items.Add(new VoiceComboItem(label, voice.Id));
            }

            if (VoiceCombo.Items.Count > 0)
            {
                VoiceCombo.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[TextToSpeechPage] Failed to load voices: {0}", ex.Message);
            StatusText.Text = "Failed to load TTS model. Check that model files are present.";
        }

        UpdateButtonStates();
    }

    private void SpeechTextInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateButtonStates();
    }

    private void VoiceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        bool hasText = !string.IsNullOrWhiteSpace(SpeechTextInput?.Text);
        bool hasVoice = VoiceCombo?.SelectedItem is VoiceComboItem;

        if (PlayButton is not null)
            PlayButton.IsEnabled = hasText && hasVoice && !_isSpeaking;

        if (StopButton is not null)
            StopButton.IsEnabled = _isSpeaking;

        if (PreviewVoiceButton is not null)
            PreviewVoiceButton.IsEnabled = hasVoice && !_isSpeaking;
    }

    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        var text = SpeechTextInput.Text.Trim();
        if (string.IsNullOrEmpty(text))
            return;

        await SpeakTextAsync(text);
    }

    private async void PreviewVoiceButton_Click(object sender, RoutedEventArgs e)
    {
        await SpeakTextAsync(PreviewPhrase);
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        CancelPlayback();
    }

    private async Task SpeakTextAsync(string text)
    {
        if (VoiceCombo.SelectedItem is not VoiceComboItem selectedVoice)
            return;

        // Cancel any existing playback
        CancelPlayback();

        _cts = new CancellationTokenSource();
        SetSpeakingState(true);
        StatusText.Text = "Generating speech...";

        try
        {
            await _ttsService.SpeakAsync(text, selectedVoice.VoiceId, cancellationToken: _cts.Token);
            StatusText.Text = "Playback complete.";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Playback stopped.";
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TextToSpeechPage] SpeakAsync failed: {0}", ex.Message);
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            SetSpeakingState(false);
        }
    }

    private void CancelPlayback()
    {
        if (_cts is not null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }

    private void SetSpeakingState(bool speaking)
    {
        _isSpeaking = speaking;
        GenerationProgress.Visibility = speaking ? Visibility.Visible : Visibility.Collapsed;
        UpdateButtonStates();
    }

    /// <summary>
    /// Display item for the voice combo box.
    /// </summary>
    private sealed record VoiceComboItem(string DisplayName, string VoiceId)
    {
        public override string ToString() => DisplayName;
    }
}
