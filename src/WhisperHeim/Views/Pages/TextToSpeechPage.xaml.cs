using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using WhisperHeim.Services.Audio;
using WhisperHeim.Services.Settings;
using WhisperHeim.Services.TextToSpeech;

namespace WhisperHeim.Views.Pages;

/// <summary>
/// Merged Text-to-Speech page: TTS playback + mic voice cloning + system audio voice capture.
/// </summary>
public partial class TextToSpeechPage : UserControl
{
    private const string PreviewPhrase = "Hello, this is a voice preview.";
    private const double MinimumDurationSeconds = 5.0;

    /// <summary>
    /// Directory for custom voice reference .wav files (resolved from DataPathService).
    /// </summary>
    private readonly string _customVoicesDir;

    // ── TTS services ──
    private readonly ITextToSpeechService _ttsService;
    private readonly SettingsService _settingsService;
    private readonly AudioExportService _exportService = new();
    private CancellationTokenSource? _cts;
    private bool _isSpeaking;
    private TtsGenerationResult? _lastGenerationResult;

    // ── Voice cloning services ──
    private readonly IHighQualityRecorderService _recorderService;
    private readonly IHighQualityLoopbackService _loopbackService;
    private readonly DispatcherTimer _loopbackDurationTimer;

    // Clone state
    private bool _isCloneRecording;
    private bool _hasValidCloneRecording;
    private bool _useMicSource = true; // true = mic, false = system audio

    public TextToSpeechPage(
        ITextToSpeechService ttsService,
        IHighQualityRecorderService recorderService,
        IHighQualityLoopbackService loopbackService,
        SettingsService settingsService,
        DataPathService dataPathService)
    {
        _ttsService = ttsService;
        _recorderService = recorderService;
        _loopbackService = loopbackService;
        _settingsService = settingsService;
        _customVoicesDir = dataPathService.VoicesPath;

        InitializeComponent();

        // Duration update timer for loopback capture
        _loopbackDurationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _loopbackDurationTimer.Tick += LoopbackDurationTimer_Tick;

        // Wire up recorder events
        _recorderService.LevelChanged += OnRecorderLevelChanged;
        _recorderService.DurationChanged += OnRecorderDurationChanged;
        _recorderService.RecordingStopped += OnRecorderStopped;

        // Wire up loopback events
        _loopbackService.AudioDataAvailable += OnLoopbackAudioData;
        _loopbackService.CaptureStopped += OnLoopbackCaptureStopped;

        Loaded += OnLoaded;
    }

    // ════════════════════════════════════════════════════════════════
    //  TTS: Voice loading & playback (from original TextToSpeechPage)
    // ════════════════════════════════════════════════════════════════

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await PopulateVoicesAsync();
        PopulateCloneDevices();
        RefreshVoicesList();
    }

    private async Task PopulateVoicesAsync()
    {
        VoiceCombo.Items.Clear();

        try
        {
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
                // Pre-select the saved default voice, or fall back to the first voice
                var savedVoiceId = _settingsService.Current.Tts.DefaultVoiceId;
                int selectedIndex = 0;

                if (!string.IsNullOrEmpty(savedVoiceId))
                {
                    for (int i = 0; i < VoiceCombo.Items.Count; i++)
                    {
                        if (VoiceCombo.Items[i] is VoiceComboItem item && item.VoiceId == savedVoiceId)
                        {
                            selectedIndex = i;
                            break;
                        }
                    }
                }

                VoiceCombo.SelectedIndex = selectedIndex;
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

        // Persist the selected voice as the default for read-aloud hotkey
        if (VoiceCombo.SelectedItem is VoiceComboItem selected)
        {
            _settingsService.Current.Tts.DefaultVoiceId = selected.VoiceId;
            _settingsService.Save();
        }
    }

    private void UpdateButtonStates()
    {
        bool hasText = !string.IsNullOrWhiteSpace(SpeechTextInput?.Text);
        bool hasVoice = VoiceCombo?.SelectedItem is VoiceComboItem;

        if (PlayButton is not null)
            PlayButton.IsEnabled = hasText && hasVoice && !_isSpeaking;

        if (StopButton is not null)
            StopButton.IsEnabled = _isSpeaking;

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

        CancelPlayback();

        _cts = new CancellationTokenSource();
        SetSpeakingState(true);
        StatusText.Text = "Generating speech...";
        PlaybackTimeText.Text = "Generating...";

        try
        {
            await _ttsService.SpeakAsync(text, selectedVoice.VoiceId, cancellationToken: _cts.Token);
            StatusText.Text = "Playback complete.";
            PlaybackTimeText.Text = "Complete";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Playback stopped.";
            PlaybackTimeText.Text = "Stopped";
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TextToSpeechPage] SpeakAsync failed: {0}", ex.Message);
            StatusText.Text = $"Error: {ex.Message}";
            PlaybackTimeText.Text = "Error";
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

    // ════════════════════════════════════════════════════════════════
    //  Voice Cloning: Mic + System Audio (merged from two pages)
    // ════════════════════════════════════════════════════════════════

    private void SourceToggle_Changed(object sender, RoutedEventArgs e)
    {
        _useMicSource = MicSourceToggle.IsChecked == true;
        PopulateCloneDevices();
    }

    private void PopulateCloneDevices()
    {
        if (CloneDeviceCombo is null) return;

        CloneDeviceCombo.Items.Clear();

        if (_useMicSource)
        {
            CloneDeviceCombo.Items.Add(new DeviceComboItem("System Default", -1));
            try
            {
                var devices = _recorderService.GetAvailableDevices();
                foreach (var device in devices)
                {
                    CloneDeviceCombo.Items.Add(new DeviceComboItem(device.Name, device.DeviceIndex));
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("[TextToSpeechPage] Failed to enumerate mic devices: {0}", ex.Message);
            }
        }
        else
        {
            CloneDeviceCombo.Items.Add(new DeviceComboItem("System Default", -1));
            try
            {
                var devices = _loopbackService.GetAvailableDevices();
                foreach (var device in devices)
                {
                    CloneDeviceCombo.Items.Add(new DeviceComboItem(device.Name, device.DeviceIndex));
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("[TextToSpeechPage] Failed to enumerate output devices: {0}", ex.Message);
            }
        }

        CloneDeviceCombo.SelectedIndex = 0;
        CloneDeviceCombo.DisplayMemberPath = "DisplayName";
    }

    private void CloneRecordButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isCloneRecording) return;

        var selectedDevice = CloneDeviceCombo.SelectedItem as DeviceComboItem;
        int deviceIndex = selectedDevice?.DeviceIndex ?? -1;

        try
        {
            if (_useMicSource)
            {
                _recorderService.StartRecording(deviceIndex);
            }
            else
            {
                _loopbackService.StartCapture(deviceIndex);
                _loopbackDurationTimer.Start();
            }

            _isCloneRecording = true;
            _hasValidCloneRecording = false;

            CloneRecordButton.IsEnabled = false;
            CloneStopButton.IsEnabled = true;
            CloneSaveButton.IsEnabled = false;
            CloneVoiceNameBox.IsEnabled = false;
            CloneDeviceCombo.IsEnabled = false;
            MicSourceToggle.IsEnabled = false;
            SystemAudioToggle.IsEnabled = false;
            CloneStatusText.Text = _useMicSource
                ? "Recording... Speak clearly into the microphone."
                : "Capturing system audio... Play the target voice.";

            // Reset visuals
            LevelMeterFill.Width = 0;
            CloneDurationProgress.Width = 0;
            CloneDurationText.Text = "00:00";
        }
        catch (Exception ex)
        {
            CloneStatusText.Text = $"Failed to start: {ex.Message}";
            ResetCloneControls();
        }
    }

    private void CloneStopButton_Click(object sender, RoutedEventArgs e)
    {
        StopCloneRecording();
    }

    private void StopCloneRecording()
    {
        if (!_isCloneRecording) return;

        _isCloneRecording = false;

        if (_useMicSource)
        {
            var filePath = _recorderService.StopRecording();
            _hasValidCloneRecording = filePath is not null && _recorderService.Duration.TotalSeconds >= MinimumDurationSeconds;

            if (_hasValidCloneRecording)
            {
                CloneStatusText.Text = $"Recording complete ({_recorderService.Duration.TotalSeconds:F1}s). Enter a name and click Save.";
            }
            else if (_recorderService.Duration.TotalSeconds < MinimumDurationSeconds)
            {
                CloneStatusText.Text = $"Recording too short ({_recorderService.Duration.TotalSeconds:F1}s). Minimum {MinimumDurationSeconds}s required.";
            }
            else
            {
                CloneStatusText.Text = "Recording failed. Please try again.";
            }
        }
        else
        {
            _loopbackDurationTimer.Stop();
            _loopbackService.StopCapture();

            bool hasRecording = _loopbackService.TempWavFilePath is not null
                && File.Exists(_loopbackService.TempWavFilePath);
            _hasValidCloneRecording = hasRecording;

            if (hasRecording)
            {
                CloneStatusText.Text = "Capture complete. Enter a name and click Save.";
            }
            else
            {
                CloneStatusText.Text = "No audio was captured. Make sure audio is playing.";
            }
        }

        ResetCloneControls();
        LevelMeterFill.Width = 0;
        LevelText.Text = "--";
    }

    private void CloneSaveButton_Click(object sender, RoutedEventArgs e)
    {
        var voiceName = CloneVoiceNameBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(voiceName))
        {
            CloneStatusText.Text = "Please enter a voice name.";
            return;
        }

        // Sanitize filename
        foreach (var ch in Path.GetInvalidFileNameChars())
        {
            voiceName = voiceName.Replace(ch, '_');
        }

        var destPath = Path.Combine(_customVoicesDir, $"{voiceName}.wav");

        if (File.Exists(destPath))
        {
            var result = MessageBox.Show(
                $"A voice named '{voiceName}' already exists. Overwrite?",
                "Voice Already Exists",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;
        }

        try
        {
            bool saved;
            if (_useMicSource)
            {
                saved = _recorderService.SaveRecording(destPath);
            }
            else
            {
                _loopbackService.SaveAsVoice(voiceName);
                saved = true;
            }

            if (saved)
            {
                CloneStatusText.Text = $"Voice '{voiceName}' saved! It will appear in the voice selector.";
                _hasValidCloneRecording = false;
                CloneSaveButton.IsEnabled = false;
                CloneVoiceNameBox.Text = "";
                RefreshVoicesList();

                // Refresh TTS voice list to pick up the new voice
                _ = PopulateVoicesAsync();
            }
            else
            {
                CloneStatusText.Text = "Failed to save recording. Please try again.";
            }
        }
        catch (Exception ex)
        {
            CloneStatusText.Text = $"Failed to save voice: {ex.Message}";
            Trace.TraceError("[TextToSpeechPage] Save voice failed: {0}", ex);
        }
    }

    private void ResetCloneControls()
    {
        CloneRecordButton.IsEnabled = true;
        CloneStopButton.IsEnabled = false;
        CloneVoiceNameBox.IsEnabled = true;
        CloneDeviceCombo.IsEnabled = true;
        MicSourceToggle.IsEnabled = true;
        SystemAudioToggle.IsEnabled = true;
        UpdateCloneSaveButtonState();
    }

    private void CloneVoiceNameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateCloneSaveButtonState();
    }

    private void UpdateCloneSaveButtonState()
    {
        if (CloneSaveButton is null) return;
        CloneSaveButton.IsEnabled = _hasValidCloneRecording
            && !string.IsNullOrWhiteSpace(CloneVoiceNameBox.Text);
    }

    // ── Recorder event handlers (mic) ──

    private void OnRecorderLevelChanged(object? sender, float level)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!_useMicSource || !_isCloneRecording) return;
            var maxWidth = LevelMeterGrid.ActualWidth;
            LevelMeterFill.Width = maxWidth * level;
            LevelText.Text = level > 0.001f ? $"{20f * MathF.Log10(level):F1} dB" : "--";
        });
    }

    private void OnRecorderDurationChanged(object? sender, TimeSpan duration)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!_useMicSource) return;

            CloneDurationText.Text = $"{duration:mm\\:ss}";

            double progress = Math.Min(1.0, duration.TotalSeconds / MinimumDurationSeconds);
            CloneDurationProgress.Width = (CloneDurationProgress.Parent as Grid)?.ActualWidth * progress ?? 0;

            if (duration.TotalSeconds >= MinimumDurationSeconds)
            {
                CloneDurationBrush.Color = Color.FromRgb(0x4C, 0xAF, 0x50);
            }
            else
            {
                CloneDurationBrush.Color = Color.FromRgb(0xFF, 0xA5, 0x00);
            }
        });
    }

    private void OnRecorderStopped(object? sender, RecordingStoppedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!e.Success)
            {
                CloneStatusText.Text = $"Recording stopped due to an error: {e.Exception?.Message}";
            }
            _isCloneRecording = false;
            ResetCloneControls();
            LevelMeterFill.Width = 0;
        });
    }

    // ── Loopback event handlers (system audio) ──

    private void OnLoopbackAudioData(object? sender, HighQualityAudioEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_useMicSource || !_isCloneRecording) return;
            float level = Math.Clamp(e.RmsLevel * 5f, 0f, 1f);
            double maxWidth = LevelMeterGrid.ActualWidth;
            LevelMeterFill.Width = level * maxWidth;

            if (e.RmsLevel > 0.001f)
            {
                float db = 20f * MathF.Log10(e.RmsLevel);
                LevelText.Text = $"{db:F1} dB";
            }
            else
            {
                LevelText.Text = "-inf dB";
            }
        });
    }

    private void LoopbackDurationTimer_Tick(object? sender, EventArgs e)
    {
        var duration = _loopbackService.Duration;
        CloneDurationText.Text = $"{duration:mm\\:ss}";

        double progress = Math.Min(1.0, duration.TotalSeconds / MinimumDurationSeconds);
        CloneDurationProgress.Width = (CloneDurationProgress.Parent as Grid)?.ActualWidth * progress ?? 0;

        if (duration.TotalSeconds >= MinimumDurationSeconds)
        {
            CloneDurationBrush.Color = Color.FromRgb(0x4C, 0xAF, 0x50);
        }
        else
        {
            CloneDurationBrush.Color = Color.FromRgb(0xFF, 0xA5, 0x00);
        }
    }

    private void OnLoopbackCaptureStopped(object? sender, CaptureStoppedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (e.WasDeviceDisconnected)
            {
                CloneStatusText.Text = "Audio device was disconnected.";
            }
            if (_isCloneRecording)
            {
                StopCloneRecording();
            }
        });
    }

    // ── Library Voices list ──

    private void RefreshVoicesList()
    {
        if (VoicesList is null) return;

        VoicesList.Items.Clear();

        if (!Directory.Exists(_customVoicesDir))
            return;

        var wavFiles = Directory.GetFiles(_customVoicesDir, "*.wav");

        foreach (var wavFile in wavFiles)
        {
            var name = Path.GetFileNameWithoutExtension(wavFile);
            var info = new FileInfo(wavFile);

            var card = new Border
            {
                Background = (System.Windows.Media.Brush)FindResource("CardBackgroundFillColorDefaultBrush"),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = name // voice ID for selection
            };
            card.MouseLeftButtonUp += LibraryVoice_Click;

            var panel = new DockPanel();
            var nameBlock = new TextBlock
            {
                Text = name,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
            };
            DockPanel.SetDock(nameBlock, Dock.Left);

            // Delete button
            var deleteButton = new Button
            {
                Content = "✕",
                FontSize = 11,
                Padding = new Thickness(6, 2, 6, 2),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0x3A, 0x3A)),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = wavFile // store file path for deletion
            };
            deleteButton.Click += DeleteVoice_Click;
            DockPanel.SetDock(deleteButton, Dock.Right);

            var sizeBlock = new TextBlock
            {
                Text = $"{info.Length / 1024.0:F0} KB",
                Opacity = 0.5,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 8, 0)
            };
            DockPanel.SetDock(sizeBlock, Dock.Right);

            panel.Children.Add(nameBlock);
            panel.Children.Add(deleteButton);
            panel.Children.Add(sizeBlock);
            card.Child = panel;
            VoicesList.Items.Add(card);
        }
    }

    private void LibraryVoice_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: string voiceName })
            return;

        // Find and select the matching voice in the combo box
        for (int i = 0; i < VoiceCombo.Items.Count; i++)
        {
            if (VoiceCombo.Items[i] is VoiceComboItem item && item.VoiceId == $"custom:{voiceName}")
            {
                VoiceCombo.SelectedIndex = i;
                // VoiceCombo_SelectionChanged will persist DefaultVoiceId
                break;
            }
        }
    }

    private void DeleteVoice_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string filePath })
            return;

        var voiceName = Path.GetFileNameWithoutExtension(filePath);
        var dialog = new WhisperHeim.Views.DeleteConfirmationDialog(voiceName, "Delete Voice")
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();

        if (!dialog.Confirmed)
            return;

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Trace.TraceInformation("[TextToSpeechPage] Deleted voice: {0}", filePath);
            }

            RefreshVoicesList();
            _ = PopulateVoicesAsync();
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TextToSpeechPage] Failed to delete voice: {0}", ex.Message);
            CloneStatusText.Text = $"Failed to delete voice: {ex.Message}";
        }
    }

    // ── Public API ──

    /// <summary>
    /// Sets the TTS input workspace text programmatically.
    /// Called by the main window when the read-aloud hotkey captures text from another application.
    /// Replaces any existing text in the input workspace.
    /// </summary>
    public void SetInputText(string text)
    {
        SpeechTextInput.Text = text;
    }

    // ── Helpers ──

    /// <summary>
    /// Display item for the voice combo box.
    /// </summary>
    private sealed record VoiceComboItem(string DisplayName, string VoiceId)
    {
        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// Display item for clone device combo box.
    /// </summary>
    private sealed record DeviceComboItem(string DisplayName, int DeviceIndex)
    {
        public override string ToString() => DisplayName;
    }
}
