using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using WhisperHeim.Services.Analysis;
using WhisperHeim.Services.Settings;
using WhisperHeim.Services.Startup;

namespace WhisperHeim.Views.Pages;

public partial class GeneralPage : UserControl
{
    private readonly SettingsService _settingsService;
    private readonly OllamaService _ollamaService;
    private readonly StartupService _startupService = new();

    public GeneralPage(SettingsService settingsService, OllamaService ollamaService)
    {
        _settingsService = settingsService;
        _ollamaService = ollamaService;
        DataContext = _settingsService.Current.General;
        InitializeComponent();
        UpdateDataPathDisplay();
        InitializeOllamaSettings();

        // Highlight the active theme card once the visual tree is ready,
        // so that Background assignments are applied after layout.
        Loaded += (_, _) => HighlightActiveTheme();
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

    private void UpdateDataPathDisplay()
    {
        var dataPath = _settingsService.DataPathService.DataPath;
        DataPathDisplay.Text = dataPath;
    }

    private void BrowseDataPath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select data folder for WhisperHeim",
            InitialDirectory = _settingsService.DataPathService.DataPath,
        };

        if (dialog.ShowDialog() == true)
        {
            var newPath = dialog.FolderName;

            if (!DataPathService.ValidatePath(newPath))
            {
                MessageBox.Show(
                    $"The selected folder is not writable:\n\n{newPath}\n\nPlease choose a different folder.",
                    "Invalid Folder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (_settingsService.DataPathService.SetDataPath(newPath))
            {
                UpdateDataPathDisplay();
                MessageBox.Show(
                    "Data folder changed. Please restart WhisperHeim for the change to take full effect.",
                    "Restart Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }

    private void ResetDataPath_Click(object sender, RoutedEventArgs e)
    {
        _settingsService.DataPathService.SetDataPath(null);
        UpdateDataPathDisplay();
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

    // --- Ollama settings ---

    private void InitializeOllamaSettings()
    {
        OllamaEndpointBox.Text = _settingsService.Current.Ollama.Endpoint;

        var currentModel = _settingsService.Current.Ollama.Model;
        if (!string.IsNullOrEmpty(currentModel))
        {
            OllamaModelCombo.Items.Add(currentModel);
            OllamaModelCombo.SelectedItem = currentModel;
        }
    }

    private void OllamaEndpoint_LostFocus(object sender, RoutedEventArgs e)
    {
        var newEndpoint = OllamaEndpointBox.Text?.Trim();
        if (!string.IsNullOrEmpty(newEndpoint))
        {
            _settingsService.Current.Ollama.Endpoint = newEndpoint;
            _settingsService.Save();
        }
    }

    private async void TestOllama_Click(object sender, RoutedEventArgs e)
    {
        // Save endpoint first
        OllamaEndpoint_LostFocus(sender, e);

        OllamaStatusText.Text = "Testing...";
        OllamaStatusText.Foreground = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString("#FF888888"));

        var connected = await _ollamaService.TestConnectionAsync();

        if (connected)
        {
            OllamaStatusText.Text = "Connected";
            OllamaStatusText.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#FF00AA00"));

            // Auto-refresh models on successful connection
            await RefreshModelsAsync();
        }
        else
        {
            OllamaStatusText.Text = "Not reachable";
            OllamaStatusText.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#FFE74856"));
        }
    }

    private async void RefreshModels_Click(object sender, RoutedEventArgs e)
    {
        await RefreshModelsAsync();
    }

    private async Task RefreshModelsAsync()
    {
        var models = await _ollamaService.ListLocalModelsAsync();
        var currentModel = _settingsService.Current.Ollama.Model;

        OllamaModelCombo.Items.Clear();
        foreach (var model in models)
            OllamaModelCombo.Items.Add(model);

        if (models.Count == 0)
        {
            OllamaStatusText.Text = "No models found";
            OllamaStatusText.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#FFE74856"));
            return;
        }

        // Restore previous selection if it still exists
        if (!string.IsNullOrEmpty(currentModel) && models.Contains(currentModel))
            OllamaModelCombo.SelectedItem = currentModel;
        else if (models.Count > 0)
            OllamaModelCombo.SelectedIndex = 0;
    }

    private void OllamaModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OllamaModelCombo.SelectedItem is string model)
        {
            _settingsService.Current.Ollama.Model = model;
            _settingsService.Save();
            Trace.TraceInformation("[GeneralPage] Ollama model set to: {0}", model);
        }
    }
}
