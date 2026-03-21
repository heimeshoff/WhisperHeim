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

    private void TemplateList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TemplateList.SelectedItem is TemplateItem item)
        {
            NameTextBox.Text = item.Name;
            TextTextBox.Text = item.Text;
            UpdateButton.IsEnabled = true;
            DeleteButton.IsEnabled = true;
        }
        else
        {
            UpdateButton.IsEnabled = false;
            DeleteButton.IsEnabled = false;
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

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var index = TemplateList.SelectedIndex;
        if (index < 0)
            return;

        _templateService.RemoveTemplate(index);
        NameTextBox.Text = string.Empty;
        TextTextBox.Text = string.Empty;
        UpdateButton.IsEnabled = false;
        DeleteButton.IsEnabled = false;
        RefreshList();
    }
}
