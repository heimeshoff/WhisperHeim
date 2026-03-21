using System.Windows.Controls;
using System.Windows.Media;
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
            ? $"{sizeMB:F1} MB — {info.ModelDirectory}"
            : $"Expected ~{info.Definition.TotalSizeBytes / (1024.0 * 1024.0):F0} MB — {info.ModelDirectory}";
    }

    public string Name { get; }
    public string Description { get; }
    public string StatusText { get; }
    public Brush StatusBrush { get; }
    public string SizeText { get; }
}
