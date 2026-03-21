using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WhisperHeim.Services.TextToSpeech;

namespace WhisperHeim.Views.Pages;

/// <summary>
/// Text-to-speech page: enter text, select a voice, and play speech audio.
/// </summary>
public partial class TextToSpeechPage : UserControl
{
    private const string PreviewPhrase = "Hello, this is a voice preview.";

    private readonly ITextToSpeechService _ttsService;
    private readonly AudioExportService _exportService = new();
    private CancellationTokenSource? _cts;
    private bool _isSpeaking;
    private TtsGenerationResult? _lastGenerationResult;

    public TextToSpeechPage(ITextToSpeechService ttsService)
    {
        _ttsService = ttsService;

        InitializeComponent();

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await PopulateVoicesAsync();
    }

    private async Task PopulateVoicesAsync()
    {
        VoiceCombo.Items.Clear();

        try
        {
            // Load the TTS model on a background thread to avoid blocking the UI
            if (!_ttsService.IsLoaded)
            {
                StatusText.Text = "Loading TTS model...";
                await Task.Run(() => _ttsService.LoadModel());
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

            StatusText.Text = "";
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

        if (SaveAsButton is not null)
            SaveAsButton.IsEnabled = hasText && hasVoice && !_isSpeaking;
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

    private async void SaveAsButton_Click(object sender, RoutedEventArgs e)
    {
        var text = SpeechTextInput.Text.Trim();
        if (string.IsNullOrEmpty(text))
            return;

        if (VoiceCombo.SelectedItem is not VoiceComboItem selectedVoice)
            return;

        // Show save dialog first so user can cancel before generation
        var dialog = new SaveFileDialog
        {
            Title = "Save Audio As",
            Filter = "WAV files (*.wav)|*.wav|MP3 files (*.mp3)|*.mp3|OGG files (*.ogg)|*.ogg",
            DefaultExt = ".wav",
            FileName = "speech"
        };

        if (dialog.ShowDialog() != true)
            return;

        SetSpeakingState(true);
        StatusText.Text = "Generating audio...";

        try
        {
            // Generate audio (or reuse last if text hasn't changed)
            var result = await _ttsService.GenerateAudioAsync(text, selectedVoice.VoiceId);
            _lastGenerationResult = result;

            StatusText.Text = "Exporting...";

            var ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            switch (ext)
            {
                case ".wav":
                    await _exportService.ExportToWavAsync(result.Samples, result.SampleRate, dialog.FileName);
                    break;
                case ".mp3":
                    await _exportService.ExportToMp3Async(result.Samples, result.SampleRate, dialog.FileName);
                    break;
                case ".ogg":
                    await _exportService.ExportToOggAsync(result.Samples, result.SampleRate, dialog.FileName);
                    break;
                default:
                    StatusText.Text = $"Unsupported format: {ext}";
                    return;
            }

            StatusText.Text = $"Saved to {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TextToSpeechPage] Export failed: {0}", ex.Message);
            StatusText.Text = $"Export failed: {ex.Message}";
        }
        finally
        {
            SetSpeakingState(false);
        }
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
