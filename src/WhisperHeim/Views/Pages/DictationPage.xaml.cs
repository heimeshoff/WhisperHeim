using System.Windows;
using System.Windows.Controls;
using WhisperHeim.Services.Audio;
using WhisperHeim.Services.Settings;

namespace WhisperHeim.Views.Pages;

public partial class DictationPage : UserControl
{
    private readonly SettingsService _settingsService;
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly List<AudioDeviceInfo> _devices = new();
    private bool _isInitializing = true;

    /// <summary>
    /// Display item for the microphone combo box.
    /// </summary>
    private sealed record MicComboItem(string DisplayName, int DeviceIndex);

    public DictationPage(SettingsService settingsService, IAudioCaptureService audioCaptureService)
    {
        _settingsService = settingsService;
        _audioCaptureService = audioCaptureService;

        InitializeComponent();
        PopulateMicrophoneList();
        _isInitializing = false;

        TestTextBox.TextChanged += (_, _) => UpdateWatermark();
        UpdateWatermark();
    }

    private void PopulateMicrophoneList()
    {
        _devices.Clear();
        MicrophoneCombo.Items.Clear();

        // Add "System Default" as the first entry
        MicrophoneCombo.Items.Add(new MicComboItem("System Default", -1));

        try
        {
            var availableDevices = _audioCaptureService.GetAvailableDevices();
            foreach (var device in availableDevices)
            {
                _devices.Add(device);
                MicrophoneCombo.Items.Add(new MicComboItem(device.Name, device.DeviceIndex));
            }
        }
        catch
        {
            // If device enumeration fails, we still have the default option
        }

        // Restore saved selection
        var savedDeviceName = _settingsService.Current.Dictation.AudioDevice;

        if (string.IsNullOrEmpty(savedDeviceName))
        {
            // No saved device => select "System Default"
            MicrophoneCombo.SelectedIndex = 0;
        }
        else
        {
            // Find the saved device by name
            int selectedIndex = -1;
            for (int i = 0; i < MicrophoneCombo.Items.Count; i++)
            {
                if (MicrophoneCombo.Items[i] is MicComboItem item &&
                    item.DisplayName == savedDeviceName)
                {
                    selectedIndex = i;
                    break;
                }
            }

            if (selectedIndex >= 0)
            {
                MicrophoneCombo.SelectedIndex = selectedIndex;
            }
            else
            {
                // Saved device not found -- fall back to default and warn
                MicrophoneCombo.SelectedIndex = 0;
                ShowDeviceWarning(savedDeviceName);

                // Update settings to reflect the fallback
                _settingsService.Current.Dictation.AudioDevice = null;
                _settingsService.Save();
            }
        }

        // Set display member path
        MicrophoneCombo.DisplayMemberPath = "DisplayName";
    }

    private void ShowDeviceWarning(string missingDeviceName)
    {
        DeviceWarning.Text = $"Previously selected microphone \"{missingDeviceName}\" is no longer available. Falling back to system default.";
        DeviceWarningCard.Visibility = Visibility.Visible;
    }

    private void UpdateWatermark()
    {
        WatermarkText.Visibility = string.IsNullOrEmpty(TestTextBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TestTextBox.Text))
            Clipboard.SetText(TestTextBox.Text);
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        TestTextBox.Clear();
    }

    private void MicrophoneCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing)
            return;

        // Hide any previous warning when user makes a new selection
        DeviceWarningCard.Visibility = Visibility.Collapsed;

        if (MicrophoneCombo.SelectedItem is MicComboItem selected)
        {
            // Store null for system default, device name for specific devices
            _settingsService.Current.Dictation.AudioDevice =
                selected.DeviceIndex < 0 ? null : selected.DisplayName;
            _settingsService.Save();
        }
    }
}
