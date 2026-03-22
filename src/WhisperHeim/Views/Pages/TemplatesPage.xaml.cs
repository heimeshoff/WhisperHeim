using System.Windows;
using System.Windows.Controls;
using WhisperHeim.Models;
using WhisperHeim.Services.Templates;

namespace WhisperHeim.Views.Pages;

public partial class TemplatesPage : UserControl
{
    private readonly ITemplateService _templateService;

    public TemplatesPage(ITemplateService templateService)
    {
        _templateService = templateService;
        InitializeComponent();
        RefreshList();
    }

    private void RefreshList()
    {
        TemplateList.ItemsSource = null;
        TemplateList.ItemsSource = _templateService.GetTemplates();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchBox?.Text?.Trim() ?? string.Empty;
        var templates = _templateService.GetTemplates();

        if (string.IsNullOrEmpty(searchText))
        {
            TemplateList.ItemsSource = templates;
        }
        else
        {
            TemplateList.ItemsSource = templates
                .Where(t => t.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || t.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    private void TemplateList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TemplateList.SelectedItem is TemplateItem item)
        {
            NameTextBox.Text = item.Name;
            TextTextBox.Text = item.Text;
            UpdateButton.IsEnabled = true;
        }
        else
        {
            UpdateButton.IsEnabled = false;
        }
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text?.Trim();
        var text = TextTextBox.Text?.Trim();

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(text))
            return;

        _templateService.AddTemplate(name, text);
        NameTextBox.Text = string.Empty;
        TextTextBox.Text = string.Empty;
        RefreshList();
    }

    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        var index = TemplateList.SelectedIndex;
        if (index < 0)
            return;

        var name = NameTextBox.Text?.Trim();
        var text = TextTextBox.Text?.Trim();

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(text))
            return;

        _templateService.UpdateTemplate(index, name, text);
        RefreshList();
    }

    private void DeleteTemplateItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TemplateItem item)
            return;

        e.Handled = true; // Prevent the click from selecting the item

        var templates = _templateService.GetTemplates();
        var index = -1;
        for (var i = 0; i < templates.Count; i++)
        {
            if (ReferenceEquals(templates[i], item))
            {
                index = i;
                break;
            }
        }
        if (index < 0)
            return;

        var dialog = new DeleteConfirmationDialog(item.Name, "Delete Template")
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();

        if (!dialog.Confirmed)
            return;

        _templateService.RemoveTemplate(index);

        // Clear the editor if the deleted template was selected
        if (TemplateList.SelectedItem == item)
        {
            NameTextBox.Text = string.Empty;
            TextTextBox.Text = string.Empty;
            UpdateButton.IsEnabled = false;
        }

        RefreshList();
    }
}
