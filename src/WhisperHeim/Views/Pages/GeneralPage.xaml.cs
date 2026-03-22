using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using WhisperHeim.Services.Models;
using WhisperHeim.Services.Settings;
using WhisperHeim.Services.Startup;

namespace WhisperHeim.Views.Pages;

public partial class GeneralPage : UserControl
{
    private readonly SettingsService _settingsService;
    private readonly StartupService _startupService = new();

    public GeneralPage(SettingsService settingsService, ModelManagerService modelManager)
    {
        _settingsService = settingsService;
        DataContext = _settingsService.Current.General;
        InitializeComponent();
        LoadModelStatus(modelManager);
        HighlightActiveTheme();
    }

    private void LoadModelStatus(ModelManagerService modelManager)
    {
        var statuses = modelManager.CheckAllModels();
        var items = statuses.Select(s => new ModelStatusViewModel(s)).ToList();
        ModelList.ItemsSource = items;
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        _settingsService.Save();
        _startupService.SetEnabled(_settingsService.Current.General.LaunchAtStartup);
    }

    private void ThemeLight_Click(object sender, MouseButtonEventArgs e) => ApplyTheme("Light");
    private void ThemeDark_Click(object sender, MouseButtonEventArgs e) => ApplyTheme("Dark");
    private void ThemeSystem_Click(object sender, MouseButtonEventArgs e) => ApplyTheme("System");

    private void ApplyTheme(string theme)
    {
        _settingsService.Current.General.Theme = theme;
        _settingsService.Save();

        var appTheme = theme switch
        {
            "Light" => ApplicationTheme.Light,
            "Dark" => ApplicationTheme.Dark,
            _ => ApplicationTheme.Unknown // System
        };

        if (appTheme == ApplicationTheme.Unknown)
        {
            // Follow system theme
            ApplicationThemeManager.ApplySystemTheme();
        }
        else
        {
            ApplicationThemeManager.Apply(appTheme);
        }

        HighlightActiveTheme();
    }

    private void HighlightActiveTheme()
    {
        var current = _settingsService.Current.General.Theme;
        var selectedBrush = new SolidColorBrush(Color.FromArgb(0x19, 0x00, 0x5F, 0xAA)); // subtle blue highlight
        var transparentBrush = Brushes.Transparent;

        ThemeLight.Background = current == "Light" ? selectedBrush : transparentBrush;
        ThemeDark.Background = current == "Dark" ? selectedBrush : transparentBrush;
        ThemeSystem.Background = current == "System" ? selectedBrush : transparentBrush;
    }
}

/// <summary>
/// View model for displaying a model's status on the Settings page.
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
            ? $"{sizeMB:F0} MB"
            : $"~{info.Definition.TotalSizeBytes / (1024.0 * 1024.0):F0} MB";
    }

    public string Name { get; }
    public string Description { get; }
    public string StatusText { get; }
    public Brush StatusBrush { get; }
    public string SizeText { get; }
}
