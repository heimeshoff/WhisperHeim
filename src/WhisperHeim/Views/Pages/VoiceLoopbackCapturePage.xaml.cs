using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WhisperHeim.Services.Audio;

namespace WhisperHeim.Views.Pages;

public partial class VoiceLoopbackCapturePage : UserControl
{
    private readonly IHighQualityLoopbackService _loopbackService;
    private readonly DispatcherTimer _durationTimer;

    /// <summary>Minimum required recording duration for a usable voice reference.</summary>
    private static readonly TimeSpan MinimumDuration = TimeSpan.FromSeconds(5);

    /// <summary>Display item for the device combo box.</summary>
    private sealed record DeviceComboItem(string DisplayName, int DeviceIndex);

    public VoiceLoopbackCapturePage(IHighQualityLoopbackService loopbackService)
    {
        _loopbackService = loopbackService;

        InitializeComponent();
        PopulateDeviceList();

        // Duration update timer
        _durationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _durationTimer.Tick += DurationTimer_Tick;

        // Subscribe to audio events for level metering
        _loopbackService.AudioDataAvailable += OnAudioDataAvailable;
        _loopbackService.CaptureStopped += OnCaptureStopped;
    }

    private void PopulateDeviceList()
    {
        DeviceCombo.Items.Clear();
        DeviceCombo.Items.Add(new DeviceComboItem("System Default", -1));

        try
        {
            var devices = _loopbackService.GetAvailableDevices();
            foreach (var device in devices)
            {
                DeviceCombo.Items.Add(new DeviceComboItem(device.Name, device.DeviceIndex));
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[VoiceLoopback] Failed to enumerate devices: {0}", ex.Message);
        }

        DeviceCombo.SelectedIndex = 0;
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var selectedDevice = DeviceCombo.SelectedItem as DeviceComboItem;
            int deviceIndex = selectedDevice?.DeviceIndex ?? -1;

            _loopbackService.StartCapture(deviceIndex);

            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            SaveButton.IsEnabled = false;
            DeviceCombo.IsEnabled = false;

            StatusText.Visibility = Visibility.Collapsed;
            SaveResultText.Visibility = Visibility.Collapsed;

            _durationTimer.Start();
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to start capture: {ex.Message}", isError: true);
            Trace.TraceError("[VoiceLoopback] Start failed: {0}", ex);
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        StopCapture();
    }

    private void StopCapture()
    {
        _durationTimer.Stop();
        _loopbackService.StopCapture();

        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        DeviceCombo.IsEnabled = true;

        // Enable save only if we have enough audio
        var duration = _loopbackService.Duration;
        // Duration returns zero once stopped, so check the file exists
        bool hasRecording = _loopbackService.TempWavFilePath is not null
            && System.IO.File.Exists(_loopbackService.TempWavFilePath);

        if (hasRecording)
        {
            SaveButton.IsEnabled = true;
        }
        else
        {
            ShowStatus("No audio was captured. Make sure audio is playing through the selected device.", isError: true);
        }

        // Reset level meter
        LevelMeterFill.Width = 0;
        LevelText.Text = "--";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var voiceName = VoiceNameBox.Text?.Trim();
        if (string.IsNullOrEmpty(voiceName))
        {
            ShowStatus("Please enter a name for the voice.", isError: true);
            return;
        }

        try
        {
            var savedPath = _loopbackService.SaveAsVoice(voiceName);
            SaveButton.IsEnabled = false;

            SaveResultText.Text = $"Voice \"{voiceName}\" saved successfully. It will appear in the TTS voice selector.";
            SaveResultText.Foreground = System.Windows.Media.Brushes.LightGreen;
            SaveResultText.Visibility = Visibility.Visible;
            StatusText.Visibility = Visibility.Collapsed;

            Trace.TraceInformation("[VoiceLoopback] Voice saved: {0}", savedPath);
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to save voice: {ex.Message}", isError: true);
            Trace.TraceError("[VoiceLoopback] Save failed: {0}", ex);
        }
    }

    private void DurationTimer_Tick(object? sender, EventArgs e)
    {
        var duration = _loopbackService.Duration;
        DurationText.Text = $"Duration: {duration:mm\\:ss}";

        // Warn if under minimum duration
        if (duration < MinimumDuration)
        {
            var remaining = MinimumDuration - duration;
            ShowStatus($"Keep recording... need {remaining.Seconds} more second(s) for a usable reference.", isError: false);
        }
        else
        {
            StatusText.Visibility = Visibility.Collapsed;
        }
    }

    private void OnAudioDataAvailable(object? sender, HighQualityAudioEventArgs e)
    {
        // Dispatch to UI thread for level meter update
        Dispatcher.BeginInvoke(() =>
        {
            // Scale RMS to a visual width (0-1 range, clamped)
            float level = Math.Clamp(e.RmsLevel * 5f, 0f, 1f); // Amplify for visibility
            double maxWidth = LevelMeterFill.Parent is Grid grid ? grid.ActualWidth : 300;
            LevelMeterFill.Width = level * maxWidth;

            // Show dB-ish display
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

    private void OnCaptureStopped(object? sender, CaptureStoppedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (e.WasDeviceDisconnected)
            {
                ShowStatus("Audio device was disconnected.", isError: true);
            }

            StopCapture();
        });
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError
            ? System.Windows.Media.Brushes.OrangeRed
            : System.Windows.Media.Brushes.Orange;
        StatusText.Visibility = Visibility.Visible;
    }
}
