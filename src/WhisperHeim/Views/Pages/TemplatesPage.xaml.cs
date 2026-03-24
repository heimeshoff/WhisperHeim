using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WhisperHeim.Models;
using WhisperHeim.Services.Templates;

namespace WhisperHeim.Views.Pages;

public partial class TemplatesPage : UserControl
{
    private readonly ITemplateService _templateService;
    private TemplateItem? _selectedItem;
    private int _selectedIndex = -1;
    private bool _isNewMode;

    public TemplatesPage(ITemplateService templateService)
    {
        _templateService = templateService;
        InitializeComponent();
        RefreshList();
    }

    private void RefreshList()
    {
        var templates = GetFilteredTemplates();
        TemplateList.ItemsSource = null;
        TemplateList.ItemsSource = templates;
        EmptyState.Visibility = templates.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
        RefreshList();
    }

    private void TemplateRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TemplateItem item)
            return;

        // Find the index in the full (unfiltered) list
        var templates = _templateService.GetTemplates();
        _selectedIndex = -1;
        for (var i = 0; i < templates.Count; i++)
        {
            if (ReferenceEquals(templates[i], item))
            {
                _selectedIndex = i;
                break;
            }
        }

        _selectedItem = item;
        _isNewMode = false;
        OpenDrawer(item.Name, item.Text, isNew: false);
    }

    private void AddNewButton_Click(object sender, RoutedEventArgs e)
    {
        _selectedItem = null;
        _selectedIndex = -1;
        _isNewMode = true;
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

        // Focus the name field
        NameTextBox.Focus();
    }

    private void CloseDrawer()
    {
        DrawerOverlay.Visibility = Visibility.Collapsed;
        DrawerPanel.Visibility = Visibility.Collapsed;
        _selectedItem = null;
        _selectedIndex = -1;
        _isNewMode = false;
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
            _templateService.AddTemplate(name, text);
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
}
