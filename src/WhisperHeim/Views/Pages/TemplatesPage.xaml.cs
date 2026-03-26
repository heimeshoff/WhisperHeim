using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WhisperHeim.Models;
using WhisperHeim.Services.Templates;

namespace WhisperHeim.Views.Pages;

public partial class TemplatesPage : UserControl
{
    private readonly ITemplateService _templateService;
    private TemplateItem? _selectedItem;
    private int _selectedIndex = -1;
    private bool _isNewMode;
    private string? _newTemplateTargetGroup;
    private readonly List<TemplateGroupDisplayModel> _groups = new();
    private bool _allExpanded = true;

    // Drag-and-drop state
    private Point _dragStartPoint;
    private object? _dragSource;

    public TemplatesPage(ITemplateService templateService)
    {
        _templateService = templateService;
        _templateService.EnsureDefaults();
        InitializeComponent();
        RefreshList();
    }

    private void RefreshList()
    {
        var searchText = SearchBox?.Text?.Trim() ?? string.Empty;
        var hasSearch = !string.IsNullOrEmpty(searchText);

        var allTemplates = _templateService.GetTemplates();
        var groups = _templateService.GetGroups();

        _groups.Clear();

        foreach (var group in groups)
        {
            var groupName = group.Name;
            var isUngrouped = groupName == TemplateService.UngroupedName;

            // Get templates for this group
            var templates = allTemplates
                .Select((t, i) => (Template: t, Index: i))
                .Where(x => isUngrouped
                    ? string.IsNullOrEmpty(x.Template.Group)
                    : string.Equals(x.Template.Group, groupName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Apply search filter
            if (hasSearch)
            {
                templates = templates
                    .Where(x => x.Template.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                        || x.Template.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Skip groups with no matching templates (unless group itself matches)
                if (templates.Count == 0 && !groupName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            var items = templates.Select(x => new TemplateDisplayItem(x.Template, x.Index)).ToList();

            // When searching, auto-expand groups with matches
            var isExpanded = hasSearch ? true : group.IsExpanded;

            _groups.Add(new TemplateGroupDisplayModel(
                groupName, items, isExpanded, isUngrouped));
        }

        TemplateGroupList.ItemsSource = null;
        TemplateGroupList.ItemsSource = _groups;

        var totalTemplates = _groups.Sum(g => g.Items.Count);
        EmptyState.Visibility = totalTemplates == 0 && !hasSearch
            ? Visibility.Visible : Visibility.Collapsed;

        UpdateExpandCollapseIcon();
    }

    private void UpdateExpandCollapseIcon()
    {
        _allExpanded = _groups.All(g => g.IsExpanded);
        if (ExpandCollapseAllButton?.ToolTip is not null)
            ExpandCollapseAllButton.ToolTip = _allExpanded ? "Collapse All" : "Expand All";
    }

    // --- Search ---

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshList();
    }

    // --- Expand/Collapse All ---

    private void ExpandCollapseAll_Click(object sender, RoutedEventArgs e)
    {
        var newState = !_allExpanded;
        foreach (var group in _groups)
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
            _templateService.SetGroupExpanded(group.GroupName, group.IsExpanded);
            UpdateExpandCollapseIcon();
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
        RefreshList();
    }

    // --- Delete Group ---

    private void DeleteGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TemplateGroupDisplayModel group })
            return;

        if (group.IsUngrouped) return;

        var deleted = _templateService.RemoveGroup(group.GroupName);
        if (deleted)
            RefreshList();
    }

    // --- Add Template to Group ---

    private void AddTemplateToGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TemplateGroupDisplayModel group })
            return;

        _selectedItem = null;
        _selectedIndex = -1;
        _isNewMode = true;
        _newTemplateTargetGroup = group.IsUngrouped ? null : group.GroupName;
        OpenDrawer("", "", isNew: true);
    }

    // --- Template row click ---

    private void TemplateRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TemplateDisplayItem displayItem)
            return;

        _selectedIndex = displayItem.OriginalIndex;
        _selectedItem = displayItem.Template;
        _isNewMode = false;
        _newTemplateTargetGroup = null;
        OpenDrawer(displayItem.Template.Name, displayItem.Template.Text, isNew: false);
    }

    // --- Drawer ---

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
        _selectedItem = null;
        _selectedIndex = -1;
        _isNewMode = false;
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

        if (_isNewMode)
        {
            _templateService.AddTemplate(name, text, _newTemplateTargetGroup);
        }
        else if (_selectedIndex >= 0)
        {
            _templateService.UpdateTemplate(_selectedIndex, name, text);
        }

        CloseDrawer();
        RefreshList();
    }

    private void DrawerDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedItem is null || _selectedIndex < 0)
            return;

        var dialog = new DeleteConfirmationDialog(_selectedItem.Name, "Delete Template")
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();

        if (!dialog.Confirmed)
            return;

        _templateService.RemoveTemplate(_selectedIndex);
        CloseDrawer();
        RefreshList();
    }

    // --- Inline rename (double-click group name) ---

    private void GroupName_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;

        if (sender is not FrameworkElement element || element.DataContext is not TemplateGroupDisplayModel group)
            return;

        if (group.IsUngrouped) return;

        var dialog = new InputDialog("Rename Group", "Enter new group name:", group.GroupName)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();

        if (!dialog.Confirmed || string.IsNullOrWhiteSpace(dialog.InputText))
            return;

        _templateService.RenameGroup(group.GroupName, dialog.InputText.Trim());
        RefreshList();
        e.Handled = true;
    }

    // --- Drag-and-drop: Templates between groups ---

    private void TemplateRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
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

        var data = new DataObject("TemplateDisplayItem", item);
        DragDrop.DoDragDrop(element, data, DragDropEffects.Move);
        _dragSource = null;
    }

    private void GroupDrop_Handler(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("TemplateDisplayItem") && !e.Data.GetDataPresent("TemplateGroupDrag"))
            return;

        // Handle template drop onto group
        if (e.Data.GetDataPresent("TemplateDisplayItem"))
        {
            if (sender is not FrameworkElement element)
                return;

            // Walk up to find the group DataContext
            var targetGroup = FindGroupFromElement(element);
            if (targetGroup is null) return;

            var item = e.Data.GetData("TemplateDisplayItem") as TemplateDisplayItem;
            if (item is null) return;

            var targetGroupName = targetGroup.IsUngrouped ? null : targetGroup.GroupName;
            _templateService.MoveTemplateToGroup(item.OriginalIndex, targetGroupName);
            RefreshList();
            e.Handled = true;
        }
        // Handle group reorder drop
        else if (e.Data.GetDataPresent("TemplateGroupDrag"))
        {
            var sourceGroup = e.Data.GetData("TemplateGroupDrag") as TemplateGroupDisplayModel;
            if (sourceGroup is null || sourceGroup.IsUngrouped) return;

            if (sender is not FrameworkElement element) return;
            var targetGroup = FindGroupFromElement(element);
            if (targetGroup is null || targetGroup.IsUngrouped) return;

            // Reorder: move source to target's position
            var names = _groups.Where(g => !g.IsUngrouped).Select(g => g.GroupName).ToList();
            var sourceIdx = names.IndexOf(sourceGroup.GroupName);
            var targetIdx = names.IndexOf(targetGroup.GroupName);
            if (sourceIdx < 0 || targetIdx < 0 || sourceIdx == targetIdx) return;

            names.RemoveAt(sourceIdx);
            names.Insert(targetIdx, sourceGroup.GroupName);
            _templateService.ReorderGroups(names);
            RefreshList();
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

        // Cannot drag the Ungrouped group
        if (group.IsUngrouped) return;

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
}

/// <summary>
/// Display model for a template group in the list.
/// Mirrors TranscriptGroupViewModel pattern from TranscriptsPage.
/// </summary>
internal sealed class TemplateGroupDisplayModel : INotifyPropertyChanged
{
    private bool _isExpanded;

    public TemplateGroupDisplayModel(string groupName, List<TemplateDisplayItem> items, bool isExpanded, bool isUngrouped)
    {
        GroupName = groupName;
        Items = items;
        _isExpanded = isExpanded;
        IsUngrouped = isUngrouped;
    }

    public string GroupName { get; }
    public List<TemplateDisplayItem> Items { get; }
    public bool IsUngrouped { get; }
    public string CountDisplay => $"({Items.Count})";

    /// <summary>Show delete icon only for custom (non-Ungrouped) empty groups.</summary>
    public bool ShowDeleteIcon => !IsUngrouped && Items.Count == 0;

    /// <summary>Show IBeam cursor for renameable groups.</summary>
    public Cursor RenameCursor => IsUngrouped ? Cursors.Arrow : Cursors.Hand;

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
    }

    public TemplateItem Template { get; }
    public int OriginalIndex { get; }

    // Convenience binding properties
    public string Name => Template.Name;
    public string Text => Template.Text;
}
