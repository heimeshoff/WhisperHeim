using System.Windows;
using System.Windows.Controls;
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
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        _settingsService.Save();

        // Sync the Windows auto-start registry entry with the setting
        _startupService.SetEnabled(_settingsService.Current.General.LaunchAtStartup);
    }
}
