using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
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

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void KofiButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://ko-fi.com/heimeshoff") { UseShellExecute = true });
    }
}

/// <summary>
/// View model for displaying a model's status on the About page.
/// </summary>
internal sealed class ModelStatusViewModel
{
    public ModelStatusViewModel(ModelStatusInfo info)
    {
        Name = info.Definition.Name;
        Description = info.Definition.Description;
        ProjectUrl = info.Definition.ProjectUrl;

        StatusText = info.Status switch
        {
            ModelStatus.Ready => "Ready",
            ModelStatus.Incomplete => "Incomplete",
            ModelStatus.Missing => "Missing",
            _ => "Unknown"
        };

        StatusBrush = info.Status switch
        {
            ModelStatus.Ready => Brushes.LimeGreen,
            ModelStatus.Incomplete => Brushes.Orange,
            _ => Brushes.Red
        };

        var sizeMB = info.DownloadedBytes / (1024.0 * 1024.0);
        SizeText = info.Status == ModelStatus.Ready
            ? $"{sizeMB:F0} MB"
            : $"~{info.Definition.TotalSizeBytes / (1024.0 * 1024.0):F0} MB";
    }

    public string Name { get; }
    public string Description { get; }
    public string? ProjectUrl { get; }
    public string StatusText { get; }
    public Brush StatusBrush { get; }
    public string SizeText { get; }
}
