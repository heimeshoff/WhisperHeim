using System.Windows;

namespace WhisperHeim;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Create and show the main window (it will start hidden via WindowState)
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
    }
}
