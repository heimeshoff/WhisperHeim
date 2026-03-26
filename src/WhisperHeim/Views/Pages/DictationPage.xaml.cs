using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WhisperHeim.Models;
using WhisperHeim.Services.Audio;
using WhisperHeim.Services.Settings;
using WhisperHeim.Services.Templates;
using WhisperHeim.Views;

namespace WhisperHeim.Views.Pages;

public partial class DictationPage : UserControl
{
    private readonly SettingsService _settingsService;
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly ITemplateService _templateService;
    private readonly List<AudioDeviceInfo> _devices = new();
    private bool _isInitializing = true;

    // Template drawer state
    private TemplateItem? _selectedTemplate;
    private int _selectedTemplateIndex = -1;
    private bool _isNewTemplateMode;

    /// <summary>
    /// Minimum width (in pixels) before switching to narrow/stacked layout.
    /// Chosen so the hotkeys card never shrinks below its natural content width.
    /// </summary>
    private const double NarrowBreakpoint = 640;

    private bool _isNarrowLayout;

    /// <summary>
    /// Display item for the microphone combo box.
    /// </summary>
    private sealed record MicComboItem(string DisplayName, int DeviceIndex);

    public DictationPage(SettingsService settingsService, IAudioCaptureService audioCaptureService, ITemplateService templateService)
    {
        _settingsService = settingsService;
        _audioCaptureService = audioCaptureService;
        _templateService = templateService;

        InitializeComponent();
        PopulateMicrophoneList();
        _isInitializing = false;

        TestTextBox.TextChanged += (_, _) => UpdateWatermark();
        UpdateWatermark();
        RefreshTemplateList();
    }

    // ────────────────────────────────────────────────
    //  Responsive layout
    // ────────────────────────────────────────────────

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool shouldBeNarrow = e.NewSize.Width < NarrowBreakpoint;

        if (shouldBeNarrow == _isNarrowLayout)
            return;

        _isNarrowLayout = shouldBeNarrow;
        ApplyLayout();
    }

    private void ApplyLayout()
    {
        if (_isNarrowLayout)
        {
            ApplyNarrowLayout();
        }
        else
        {
            ApplyWideLayout();
        }
    }

    /// <summary>
    /// Narrow / stacked layout: all cards full-width, stacked vertically.
    /// </summary>
    private void ApplyNarrowLayout()
    {
        // Audio input row: warning card stacks below
        AudioInputCol.Width = new GridLength(1, GridUnitType.Star);
        AudioInputGapCol.Width = new GridLength(0);
        AudioInputWarningCol.Width = new GridLength(0);

        // Move warning card to row 1, spanning full width
        Grid.SetColumn(DeviceWarningCard, 0);
        Grid.SetRow(DeviceWarningCard, 1);
        Grid.SetColumnSpan(DeviceWarningCard, 3);
        DeviceWarningCard.Margin = new Thickness(0, 12, 0, 0);

        // Bento grid: stack vertically
        BentoLeftCol.Width = new GridLength(1, GridUnitType.Star);
        BentoGapCol.Width = new GridLength(0);
        BentoRightCol.Width = new GridLength(0);

        // Move hotkeys panel to row 1, spanning full width
        Grid.SetColumn(HotkeysPanel, 0);
        Grid.SetRow(HotkeysPanel, 1);
        Grid.SetColumnSpan(HotkeysPanel, 3);
        HotkeysPanel.Margin = new Thickness(0, 24, 0, 0);

        // Transcription panel full width
        Grid.SetColumnSpan(TranscriptionPanel, 3);
    }

    /// <summary>
    /// Wide / side-by-side layout: original bento grid arrangement.
    /// </summary>
    private void ApplyWideLayout()
    {
        // Audio input row: only allocate space for warning when it's visible
        AudioInputCol.Width = new GridLength(1, GridUnitType.Star);
        UpdateWarningColumnWidths();

        // Warning card back to column 2, row 0
        Grid.SetColumn(DeviceWarningCard, 2);
        Grid.SetRow(DeviceWarningCard, 0);
        Grid.SetColumnSpan(DeviceWarningCard, 1);
        DeviceWarningCard.Margin = new Thickness(0);

        // Bento grid: 8:4 side-by-side with matching gap
        BentoLeftCol.Width = new GridLength(8, GridUnitType.Star);
        BentoGapCol.Width = new GridLength(24);
        BentoRightCol.Width = new GridLength(4, GridUnitType.Star);

        // Hotkeys panel back to column 2, row 0
        Grid.SetColumn(HotkeysPanel, 2);
        Grid.SetRow(HotkeysPanel, 0);
        Grid.SetColumnSpan(HotkeysPanel, 1);
        HotkeysPanel.Margin = new Thickness(0);

        // Transcription panel single column
        Grid.SetColumnSpan(TranscriptionPanel, 1);
    }

    /// <summary>
    /// Sets warning column widths based on whether the warning is visible and the current layout mode.
    /// </summary>
    private void UpdateWarningColumnWidths()
    {
        if (_isNarrowLayout)
            return; // Narrow layout always hides the warning column

        if (DeviceWarningCard.Visibility == Visibility.Visible)
        {
            AudioInputGapCol.Width = new GridLength(24);
            AudioInputWarningCol.Width = new GridLength(1, GridUnitType.Star);
        }
        else
        {
            AudioInputGapCol.Width = new GridLength(0);
            AudioInputWarningCol.Width = new GridLength(0);
        }
    }

    // ────────────────────────────────────────────────
    //  Microphone selection
    // ────────────────────────────────────────────────

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
        UpdateWarningColumnWidths();
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
        UpdateWarningColumnWidths();

        if (MicrophoneCombo.SelectedItem is MicComboItem selected)
        {
            // Store null for system default, device name for specific devices
            _settingsService.Current.Dictation.AudioDevice =
                selected.DeviceIndex < 0 ? null : selected.DisplayName;
            _settingsService.Save();
        }
    }

    // ────────────────────────────────────────────────
    //  Templates
    // ────────────────────────────────────────────────

    private void RefreshTemplateList()
    {
        var templates = GetFilteredTemplates();
        TemplateList.ItemsSource = null;
        TemplateList.ItemsSource = templates;
        TemplateEmptyState.Visibility = templates.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private IReadOnlyList<TemplateItem> GetFilteredTemplates()
    {
        var searchText = SearchBox?.Text?.Trim() ?? string.Empty;
        var templates = _templateService.GetTemplates();

        if (string.IsNullOrEmpty(searchText))
            return templates;

        return templates
            .Where(t => t.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                || t.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshTemplateList();
    }

    private void TemplateRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TemplateItem item)
            return;

        var templates = _templateService.GetTemplates();
        _selectedTemplateIndex = -1;
        for (var i = 0; i < templates.Count; i++)
        {
            if (ReferenceEquals(templates[i], item))
            {
                _selectedTemplateIndex = i;
                break;
            }
        }

        _selectedTemplate = item;
        _isNewTemplateMode = false;
        OpenDrawer(item.Name, item.Text, isNew: false);
    }

    private void AddNewButton_Click(object sender, RoutedEventArgs e)
    {
        _selectedTemplate = null;
        _selectedTemplateIndex = -1;
        _isNewTemplateMode = true;
        OpenDrawer("", "", isNew: true);
    }

    private void OpenDrawer(string name, string text, bool isNew)
    {
        NameTextBox.Text = name;
        TextTextBox.Text = text;
        DrawerTitle.Text = isNew ? "New Template" : "Edit Template";
        SaveButtonText.Text = isNew ? "Add Template" : "Update Template";
        DrawerDeleteButton.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;
        DrawerOverlay.Visibility = Visibility.Visible;
        DrawerPanel.Visibility = Visibility.Visible;
        AnimateDrawer(open: true);
        NameTextBox.Focus();
    }

    private void AnimateDrawer(bool open)
    {
        var anim = new DoubleAnimation
        {
            To = open ? 0 : 440,
            Duration = TimeSpan.FromMilliseconds(250),
            EasingFunction = open
                ? new CubicEase { EasingMode = EasingMode.EaseOut }
                : new CubicEase { EasingMode = EasingMode.EaseIn },
        };

        if (!open)
        {
            anim.Completed += (_, _) =>
            {
                DrawerOverlay.Visibility = Visibility.Collapsed;
                DrawerPanel.Visibility = Visibility.Collapsed;
            };
        }

        DrawerTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    private void CloseDrawer()
    {
        _selectedTemplate = null;
        _selectedTemplateIndex = -1;
        _isNewTemplateMode = false;
        AnimateDrawer(open: false);
    }

    private void DrawerClose_Click(object sender, RoutedEventArgs e)
    {
        CloseDrawer();
    }

    private void DrawerOverlay_Click(object sender, MouseButtonEventArgs e)
    {
        CloseDrawer();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text?.Trim();
        var text = TextTextBox.Text?.Trim();

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(text))
            return;

        if (_isNewTemplateMode)
        {
            _templateService.AddTemplate(name, text);
        }
        else if (_selectedTemplateIndex >= 0)
        {
            _templateService.UpdateTemplate(_selectedTemplateIndex, name, text);
        }

        CloseDrawer();
        RefreshTemplateList();
    }

    private void DrawerDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTemplate is null || _selectedTemplateIndex < 0)
            return;

        var dialog = new DeleteConfirmationDialog(_selectedTemplate.Name, "Delete Template")
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();

        if (!dialog.Confirmed)
            return;

        _templateService.RemoveTemplate(_selectedTemplateIndex);
        CloseDrawer();
        RefreshTemplateList();
    }
}
