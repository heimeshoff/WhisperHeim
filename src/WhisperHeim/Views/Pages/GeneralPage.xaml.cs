using System.Windows;
using System.Windows.Controls;
using WhisperHeim.Services.Settings;

namespace WhisperHeim.Views.Pages;

public partial class GeneralPage : UserControl
{
    private readonly SettingsService _settingsService;

    public GeneralPage(SettingsService settingsService)
    {
        _settingsService = settingsService;
        DataContext = _settingsService.Current.General;
        InitializeComponent();
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        _settingsService.Save();
    }
}
