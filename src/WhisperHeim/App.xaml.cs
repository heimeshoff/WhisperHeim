using System.Windows;
using WhisperHeim.Services.Settings;

namespace WhisperHeim;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly SettingsService _settingsService = new();

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Load settings (creates file with defaults on first run)
        _settingsService.Load();

        // Create and show the main window (it will start hidden via WindowState)
        var mainWindow = new MainWindow(_settingsService);
        MainWindow = mainWindow;
    }
}
