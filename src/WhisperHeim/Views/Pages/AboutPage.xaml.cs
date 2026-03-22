using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WhisperHeim.Services.Models;

namespace WhisperHeim.Views.Pages;

public partial class AboutPage : UserControl
{
    public AboutPage(ModelManagerService modelManager)
    {
        InitializeComponent();
        LoadModelStatus(modelManager);
    }

    private void LoadModelStatus(ModelManagerService modelManager)
    {
        var statuses = modelManager.CheckAllModels();
        var items = statuses.Select(s => new ModelStatusViewModel(s)).ToList();
        ModelList.ItemsSource = items;
    }

    private void ModelCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string url } && !string.IsNullOrEmpty(url))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }
}
