using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WhisperHeim.Services.Audio;

namespace WhisperHeim.Views.Pages;

/// <summary>
/// Voice cloning page: record high-quality mic audio and save as a custom TTS voice.
/// </summary>
public partial class VoiceCloningPage : UserControl
{
    private const double MinimumDurationSeconds = 5.0;

    /// <summary>
    /// Directory for custom voice reference .wav files.
    /// </summary>
    private static readonly string CustomVoicesDir =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WhisperHeim",
            "voices");

    private readonly IHighQualityRecorderService _recorder;
    private bool _hasValidRecording;

    public VoiceCloningPage(IHighQualityRecorderService recorder)
    {
        _recorder = recorder;

        InitializeComponent();

        _recorder.LevelChanged += OnLevelChanged;
        _recorder.DurationChanged += OnDurationChanged;
        _recorder.RecordingStopped += OnRecordingStopped;

        PopulateMicrophoneList();
        RefreshVoicesList();
    }

    private void PopulateMicrophoneList()
    {
        MicrophoneCombo.Items.Clear();
        MicrophoneCombo.Items.Add(new MicComboItem("System Default", -1));

        try
        {
            var devices = _recorder.GetAvailableDevices();
            foreach (var device in devices)
            {
                MicrophoneCombo.Items.Add(new MicComboItem(device.Name, device.DeviceIndex));
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[VoiceCloningPage] Failed to enumerate devices: {0}", ex.Message);
        }

        MicrophoneCombo.SelectedIndex = 0;
    }

    private void RefreshVoicesList()
    {
        VoicesList.Items.Clear();

        if (!Directory.Exists(CustomVoicesDir))
        {
            NoVoicesText.Visibility = Visibility.Visible;
            return;
        }

        var wavFiles = Directory.GetFiles(CustomVoicesDir, "*.wav");
        NoVoicesText.Visibility = wavFiles.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var wavFile in wavFiles)
        {
            var name = Path.GetFileNameWithoutExtension(wavFile);
            var info = new FileInfo(wavFile);
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            panel.Children.Add(new TextBlock
            {
                Text = name,
                FontWeight = FontWeights.Medium,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            });
            panel.Children.Add(new TextBlock
            {
                Text = $"({info.Length / 1024.0:F0} KB)",
                Opacity = 0.5,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });
            VoicesList.Items.Add(panel);
        }
    }

    private void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        if (_recorder.IsRecording)
            return;

        var selectedItem = MicrophoneCombo.SelectedItem as MicComboItem;
        int deviceIndex = selectedItem?.DeviceIndex ?? -1;

        try
        {
            _recorder.StartRecording(deviceIndex);
            _hasValidRecording = false;

            RecordButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            SaveButton.IsEnabled = false;
            VoiceNameTextBox.IsEnabled = false;
            MicrophoneCombo.IsEnabled = false;
            StatusText.Text = "Recording... Speak clearly into the microphone.";

            // Reset visuals
            LevelMeterFill.Width = 0;
            MinDurationProgress.Width = 0;
            DurationText.Text = "Duration: 0.0s";
            MinimumIndicator.Text = "(minimum 5s required)";
            MinimumIndicator.Foreground = new SolidColorBrush(Colors.Gray);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Failed to start recording: {ex.Message}";
            RecordButton.IsEnabled = true;
            VoiceNameTextBox.IsEnabled = true;
            MicrophoneCombo.IsEnabled = true;
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_recorder.IsRecording)
            return;

        var filePath = _recorder.StopRecording();
        _hasValidRecording = filePath is not null && _recorder.Duration.TotalSeconds >= MinimumDurationSeconds;

        RecordButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        VoiceNameTextBox.IsEnabled = true;
        MicrophoneCombo.IsEnabled = true;

        if (_hasValidRecording)
        {
            StatusText.Text = $"Recording complete ({_recorder.Duration.TotalSeconds:F1}s). Enter a name and click Save.";
            UpdateSaveButtonState();
        }
        else if (_recorder.Duration.TotalSeconds < MinimumDurationSeconds)
        {
            StatusText.Text = $"Recording too short ({_recorder.Duration.TotalSeconds:F1}s). Minimum {MinimumDurationSeconds}s required.";
            SaveButton.IsEnabled = false;
        }
        else
        {
            StatusText.Text = "Recording failed. Please try again.";
            SaveButton.IsEnabled = false;
        }

        // Reset level meter
        LevelMeterFill.Width = 0;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var voiceName = VoiceNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(voiceName))
        {
            StatusText.Text = "Please enter a voice name.";
            return;
        }

        // Sanitize filename
        foreach (var ch in Path.GetInvalidFileNameChars())
        {
            voiceName = voiceName.Replace(ch, '_');
        }

        var destPath = Path.Combine(CustomVoicesDir, $"{voiceName}.wav");

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

        bool saved = _recorder.SaveRecording(destPath);

        if (saved)
        {
            StatusText.Text = $"Voice '{voiceName}' saved successfully! It will appear in the TTS voice selector.";
            _hasValidRecording = false;
            SaveButton.IsEnabled = false;
            VoiceNameTextBox.Text = "";
            RefreshVoicesList();
        }
        else
        {
            StatusText.Text = "Failed to save recording. Please try again.";
        }
    }

    private void VoiceNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSaveButtonState();
    }

    private void UpdateSaveButtonState()
    {
        if (SaveButton is null)
            return;

        SaveButton.IsEnabled = _hasValidRecording
            && !string.IsNullOrWhiteSpace(VoiceNameTextBox.Text);
    }

    private void OnLevelChanged(object? sender, float level)
    {
        Dispatcher.BeginInvoke(() =>
        {
            // Scale level to the width of the parent grid
            var parentGrid = LevelMeterFill.Parent as Grid;
            if (parentGrid is not null)
            {
                LevelMeterFill.Width = parentGrid.ActualWidth * level;
            }
        });
    }

    private void OnDurationChanged(object? sender, TimeSpan duration)
    {
        Dispatcher.BeginInvoke(() =>
        {
            DurationText.Text = $"Duration: {duration.TotalSeconds:F1}s";

            // Update minimum duration progress bar
            double progress = Math.Min(1.0, duration.TotalSeconds / MinimumDurationSeconds);
            var parentGrid = MinDurationProgress.Parent as Grid;
            if (parentGrid is not null)
            {
                MinDurationProgress.Width = parentGrid.ActualWidth * progress;
            }

            if (duration.TotalSeconds >= MinimumDurationSeconds)
            {
                MinDurationBrush.Color = Color.FromRgb(0x4C, 0xAF, 0x50); // Green
                MinimumIndicator.Text = "(minimum reached)";
                MinimumIndicator.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
            }
            else
            {
                MinDurationBrush.Color = Color.FromRgb(0xFF, 0xA5, 0x00); // Orange
                MinimumIndicator.Text = $"({MinimumDurationSeconds - duration.TotalSeconds:F0}s more needed)";
                MinimumIndicator.Foreground = new SolidColorBrush(Colors.Gray);
            }
        });
    }

    private void OnRecordingStopped(object? sender, RecordingStoppedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!e.Success)
            {
                StatusText.Text = $"Recording stopped due to an error: {e.Exception?.Message}";
            }

            RecordButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            VoiceNameTextBox.IsEnabled = true;
            MicrophoneCombo.IsEnabled = true;
            LevelMeterFill.Width = 0;
        });
    }

    /// <summary>
    /// Display item for the microphone combo box.
    /// </summary>
    private sealed record MicComboItem(string DisplayName, int DeviceIndex)
    {
        public override string ToString() => DisplayName;
    }
}
