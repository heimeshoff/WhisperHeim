using System.Windows.Controls;
using WhisperHeim.Services.Settings;

namespace WhisperHeim.Views.Pages;

public partial class TemplatesPage : UserControl
{
    public TemplatesPage(SettingsService settingsService)
    {
        DataContext = settingsService.Current.Templates;
        InitializeComponent();
    }
}
