using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    private string? _newTemplateTargetGroup;
    private readonly List<TemplateGroupDisplayModel> _templateGroups = new();
    private bool _allExpanded = true;

    // Drag-and-drop state
    private Point _dragStartPoint;
    private object? _dragSource;

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
        _templateService.EnsureDefaults();

        InitializeComponent();
        PopulateMicrophoneList();
        InitializeTextModeToggle();
        _isInitializing = false;

        TestTextBox.TextChanged += (_, _) => UpdateWatermark();
        UpdateWatermark();
        RefreshTemplateList();

        // Re-render when settings change underneath us (e.g. disk reload from
        // another machine, or a local Save() via another page).
        _settingsService.SettingsChanged += OnSettingsChanged;
        Unloaded += (_, _) => _settingsService.SettingsChanged -= OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        // Refresh language + text-mode toggle and the template list.
        _isInitializing = true;
        try
        {
            InitializeTextModeToggle();
        }
        finally
        {
            _isInitializing = false;
        }

        RefreshTemplateList();
    }

    /// <summary>
    /// Reflects the persisted <see cref="DictationTextMode"/> on the Raw/Clean
    /// radio buttons. Called during construction, before <see cref="_isInitializing"/>
    /// is flipped, so the programmatic selection does not trigger a Save.
    /// </summary>
    private void InitializeTextModeToggle()
    {
        var mode = _settingsService.Current.Dictation.TextMode;
        if (mode == DictationTextMode.Raw)
        {
            RawModeRadio.IsChecked = true;
        }
        else
        {
            CleanModeRadio.IsChecked = true;
        }
    }

    private void TextMode_Checked(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        var newMode = RawModeRadio.IsChecked == true
            ? DictationTextMode.Raw
            : DictationTextMode.Clean;

        if (_settingsService.Current.Dictation.TextMode == newMode)
            return;

        _settingsService.Current.Dictation.TextMode = newMode;
        _settingsService.Save();
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
    //  Templates (grouped)
    // ────────────────────────────────────────────────

    private void RefreshTemplateList()
    {
        var searchText = SearchBox?.Text?.Trim() ?? string.Empty;
        var hasSearch = !string.IsNullOrEmpty(searchText);

        var allTemplates = _templateService.GetTemplates();
        var groups = _templateService.GetGroups();

        _templateGroups.Clear();

        foreach (var group in groups)
        {
            var groupName = group.Name;

            // Skip the WhisperHeim system group here; it's added separately below
            if (string.Equals(groupName, SystemTemplateDefinitions.SystemGroupName, StringComparison.OrdinalIgnoreCase))
                continue;

            var isUngrouped = groupName == TemplateService.UngroupedName;

            var templates = allTemplates
                .Select((t, i) => (Template: t, Index: i))
                .Where(x => isUngrouped
                    ? string.IsNullOrEmpty(x.Template.Group)
                    : string.Equals(x.Template.Group, groupName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (hasSearch)
            {
                templates = templates
                    .Where(x => x.Template.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                        || x.Template.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (templates.Count == 0 && !groupName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            var items = templates.Select(x => new TemplateDisplayItem(x.Template, x.Index)).ToList();
            var isExpanded = hasSearch ? true : group.IsExpanded;

            _templateGroups.Add(new TemplateGroupDisplayModel(
                groupName, items, isExpanded, isUngrouped));
        }

        // Add WhisperHeim system group at the bottom
        var systemTemplates = _templateService.GetSystemTemplates();
        if (systemTemplates.Count > 0)
        {
            var systemItems = systemTemplates
                .Where(st => !hasSearch || st.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || st.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .Select(st => new TemplateDisplayItem(st))
                .ToList();

            if (!hasSearch || systemItems.Count > 0)
            {
                // Persist expand/collapse state via settings (reuse group expand infrastructure)
                var systemGroupState = _templateService.GetGroups()
                    .FirstOrDefault(g => string.Equals(g.Name, SystemTemplateDefinitions.SystemGroupName,
                        StringComparison.OrdinalIgnoreCase));
                var systemExpanded = hasSearch || (systemGroupState?.IsExpanded ?? true);

                _templateGroups.Add(new TemplateGroupDisplayModel(
                    SystemTemplateDefinitions.SystemGroupName, systemItems, systemExpanded,
                    isUngrouped: false, isSystem: true));
            }
        }

        TemplateGroupList.ItemsSource = null;
        TemplateGroupList.ItemsSource = _templateGroups;

        var totalTemplates = _templateGroups.Sum(g => g.Items.Count);
        TemplateEmptyState.Visibility = totalTemplates == 0 && !hasSearch
            ? Visibility.Visible : Visibility.Collapsed;

        UpdateExpandCollapseIcon();
    }

    private void UpdateExpandCollapseIcon()
    {
        _allExpanded = _templateGroups.All(g => g.IsExpanded);
        if (ExpandCollapseAllButton?.ToolTip is not null)
            ExpandCollapseAllButton.ToolTip = _allExpanded ? "Collapse All" : "Expand All";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshTemplateList();
    }

    // --- Expand/Collapse All ---

    private void ExpandCollapseAll_Click(object sender, RoutedEventArgs e)
    {
        var newState = !_allExpanded;
        foreach (var group in _templateGroups)
        {
            group.IsExpanded = newState;
            _templateService.SetGroupExpanded(group.GroupName, newState);
        }
        _allExpanded = newState;
        UpdateExpandCollapseIcon();
    }

    // --- Group toggle ---

    private void TemplateGroupToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TemplateGroupDisplayModel group })
        {
            group.OnPropertyChanged(nameof(TemplateGroupDisplayModel.ChevronText));

            // For the system group, ensure it exists in settings before persisting state
            if (group.IsSystem)
            {
                EnsureSystemGroupExists();
            }

            _templateService.SetGroupExpanded(group.GroupName, group.IsExpanded);
            UpdateExpandCollapseIcon();
        }
    }

    /// <summary>
    /// Ensures the WhisperHeim system group entry exists in settings for expand/collapse persistence.
    /// </summary>
    private void EnsureSystemGroupExists()
    {
        var groups = _templateService.GetGroups();
        if (!groups.Any(g => string.Equals(g.Name, SystemTemplateDefinitions.SystemGroupName,
                StringComparison.OrdinalIgnoreCase)))
        {
            _templateService.AddGroup(SystemTemplateDefinitions.SystemGroupName);
        }
    }

    // --- Add Group ---

    private void AddGroupButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new InputDialog("New Group", "Enter group name:")
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();

        if (!dialog.Confirmed || string.IsNullOrWhiteSpace(dialog.InputText))
            return;

        _templateService.AddGroup(dialog.InputText.Trim());
        RefreshTemplateList();
    }

    // --- Delete Group ---

    private void DeleteGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TemplateGroupDisplayModel group })
            return;

        if (group.IsUngrouped || group.IsSystem) return;

        var deleted = _templateService.RemoveGroup(group.GroupName);
        if (deleted)
            RefreshTemplateList();
    }

    // --- Add Template to Group ---

    private void AddTemplateToGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TemplateGroupDisplayModel group })
            return;

        if (group.IsSystem) return;

        _selectedTemplate = null;
        _selectedTemplateIndex = -1;
        _isNewTemplateMode = true;
        _newTemplateTargetGroup = group.IsUngrouped ? null : group.GroupName;
        OpenDrawer("", "", isNew: true);
    }

    // --- Inline rename (double-click group name) ---

    private void GroupName_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;

        if (sender is not FrameworkElement element || element.DataContext is not TemplateGroupDisplayModel group)
            return;

        if (group.IsUngrouped || group.IsSystem) return;

        var dialog = new InputDialog("Rename Group", "Enter new group name:", group.GroupName)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();

        if (!dialog.Confirmed || string.IsNullOrWhiteSpace(dialog.InputText))
            return;

        _templateService.RenameGroup(group.GroupName, dialog.InputText.Trim());
        RefreshTemplateList();
        e.Handled = true;
    }

    // --- Template row click ---

    private void TemplateRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TemplateDisplayItem displayItem)
            return;

        // System templates are not clickable/editable
        if (displayItem.IsSystem)
            return;

        _selectedTemplateIndex = displayItem.OriginalIndex;
        _selectedTemplate = displayItem.Template;
        _isNewTemplateMode = false;
        _newTemplateTargetGroup = null;
        OpenDrawer(displayItem.Template.Name, displayItem.Template.Text, isNew: false);
    }

    // --- Drag-and-drop: Templates between groups ---

    private void TemplateRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Suppress drag for system templates
        if (sender is FrameworkElement { DataContext: TemplateDisplayItem item } && item.IsSystem)
            return;

        _dragStartPoint = e.GetPosition(null);
        _dragSource = sender;
    }

    private void TemplateRow_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragSource != sender)
            return;

        var pos = e.GetPosition(null);
        var diff = _dragStartPoint - pos;

        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (sender is not FrameworkElement element || element.DataContext is not TemplateDisplayItem item)
            return;

        // Suppress drag for system templates
        if (item.IsSystem)
            return;

        var data = new DataObject("TemplateDisplayItem", item);
        DragDrop.DoDragDrop(element, data, DragDropEffects.Move);
        _dragSource = null;
    }

    private void GroupDrop_Handler(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("TemplateDisplayItem") && !e.Data.GetDataPresent("TemplateGroupDrag"))
            return;

        if (e.Data.GetDataPresent("TemplateDisplayItem"))
        {
            if (sender is not FrameworkElement element) return;
            var targetGroup = FindGroupFromElement(element);
            if (targetGroup is null || targetGroup.IsSystem) return;

            var item = e.Data.GetData("TemplateDisplayItem") as TemplateDisplayItem;
            if (item is null) return;

            var targetGroupName = targetGroup.IsUngrouped ? null : targetGroup.GroupName;
            _templateService.MoveTemplateToGroup(item.OriginalIndex, targetGroupName);
            RefreshTemplateList();
            e.Handled = true;
        }
        else if (e.Data.GetDataPresent("TemplateGroupDrag"))
        {
            var sourceGroup = e.Data.GetData("TemplateGroupDrag") as TemplateGroupDisplayModel;
            if (sourceGroup is null || sourceGroup.IsUngrouped || sourceGroup.IsSystem) return;

            if (sender is not FrameworkElement element) return;
            var targetGroup = FindGroupFromElement(element);
            if (targetGroup is null || targetGroup.IsUngrouped || targetGroup.IsSystem) return;

            var names = _templateGroups.Where(g => !g.IsUngrouped).Select(g => g.GroupName).ToList();
            var sourceIdx = names.IndexOf(sourceGroup.GroupName);
            var targetIdx = names.IndexOf(targetGroup.GroupName);
            if (sourceIdx < 0 || targetIdx < 0 || sourceIdx == targetIdx) return;

            names.RemoveAt(sourceIdx);
            names.Insert(targetIdx, sourceGroup.GroupName);
            _templateService.ReorderGroups(names);
            RefreshTemplateList();
            e.Handled = true;
        }
    }

    private void GroupDragOver_Handler(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("TemplateDisplayItem") || e.Data.GetDataPresent("TemplateGroupDrag"))
            e.Effects = DragDropEffects.Move;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    // --- Drag-and-drop: Group reordering ---

    private void GroupHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _dragSource = sender;
    }

    private void GroupHeader_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragSource != sender)
            return;

        var pos = e.GetPosition(null);
        var diff = _dragStartPoint - pos;

        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (sender is not FrameworkElement element || element.DataContext is not TemplateGroupDisplayModel group)
            return;

        if (group.IsUngrouped || group.IsSystem) return;

        var data = new DataObject("TemplateGroupDrag", group);
        DragDrop.DoDragDrop(element, data, DragDropEffects.Move);
        _dragSource = null;
    }

    private TemplateGroupDisplayModel? FindGroupFromElement(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is FrameworkElement fe && fe.DataContext is TemplateGroupDisplayModel group)
                return group;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
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
        _newTemplateTargetGroup = null;
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
            _templateService.AddTemplate(name, text, _newTemplateTargetGroup);
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

/// <summary>
/// Display model for a template group in the list.
/// </summary>
internal sealed class TemplateGroupDisplayModel : INotifyPropertyChanged
{
    private bool _isExpanded;

    public TemplateGroupDisplayModel(string groupName, List<TemplateDisplayItem> items, bool isExpanded, bool isUngrouped, bool isSystem = false)
    {
        GroupName = groupName;
        Items = items;
        _isExpanded = isExpanded;
        IsUngrouped = isUngrouped;
        IsSystem = isSystem;
    }

    public string GroupName { get; }
    public List<TemplateDisplayItem> Items { get; }
    public bool IsUngrouped { get; }
    public bool IsSystem { get; }
    public string CountDisplay => $"({Items.Count})";

    /// <summary>Show delete icon only for custom (non-Ungrouped, non-System) empty groups.</summary>
    public bool ShowDeleteIcon => !IsUngrouped && !IsSystem && Items.Count == 0;

    /// <summary>Show add template button only for non-system groups.</summary>
    public bool ShowAddButton => !IsSystem;

    /// <summary>Show IBeam cursor for renameable groups.</summary>
    public Cursor RenameCursor => IsUngrouped || IsSystem ? Cursors.Arrow : Cursors.Hand;

    /// <summary>Whether group header can be dragged to reorder.</summary>
    public bool AllowGroupDrag => !IsUngrouped && !IsSystem;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ChevronText));
            }
        }
    }

    /// <summary>Down chevron when expanded, right chevron when collapsed.</summary>
    public string ChevronText => IsExpanded ? "\uE70D" : "\uE70E";

    public event PropertyChangedEventHandler? PropertyChanged;

    public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Wraps a TemplateItem with its original index in the settings list.
/// </summary>
internal sealed class TemplateDisplayItem
{
    public TemplateDisplayItem(TemplateItem template, int originalIndex)
    {
        Template = template;
        OriginalIndex = originalIndex;
        IsSystem = false;
    }

    /// <summary>
    /// Creates a display item for a system template (not editable, not clickable).
    /// </summary>
    public TemplateDisplayItem(SystemTemplate systemTemplate)
    {
        Template = new TemplateItem { Name = systemTemplate.Name, Text = systemTemplate.Description };
        OriginalIndex = -1;
        IsSystem = true;
        Description = systemTemplate.Description;
    }

    public TemplateItem Template { get; }
    public int OriginalIndex { get; }
    public bool IsSystem { get; }
    public string? Description { get; }

    // Convenience binding properties
    public string Name => Template.Name;
    public string Text => IsSystem ? (Description ?? string.Empty) : Template.Text;
}
