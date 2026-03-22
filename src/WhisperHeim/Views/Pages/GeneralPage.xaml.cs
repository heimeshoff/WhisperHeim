using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using WhisperHeim.Services.Settings;
using WhisperHeim.Services.Startup;

namespace WhisperHeim.Views.Pages;

public partial class GeneralPage : UserControl
{
    private readonly SettingsService _settingsService;
    private readonly StartupService _startupService = new();

    public GeneralPage(SettingsService settingsService)
    {
        _settingsService = settingsService;
        DataContext = _settingsService.Current.General;
        InitializeComponent();
        UpdateDataPathDisplay();

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
}
