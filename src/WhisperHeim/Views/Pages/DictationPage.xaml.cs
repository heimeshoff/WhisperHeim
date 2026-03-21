using System.Windows.Controls;
using WhisperHeim.Services.Settings;

namespace WhisperHeim.Views.Pages;

public partial class DictationPage : UserControl
{
    public DictationPage(SettingsService settingsService)
    {
        DataContext = settingsService.Current.Dictation;
        InitializeComponent();
    }
}
