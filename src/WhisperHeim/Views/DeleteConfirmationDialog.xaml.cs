using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WhisperHeim.Views;

public partial class DeleteConfirmationDialog : Window
{
    public DeleteConfirmationDialog(string itemName, string title = "Delete Recording")
    {
        InitializeComponent();
        TitleText.Text = title;
        TranscriptNameText.Text = itemName;

        Loaded += OnLoaded;
    }

    /// <summary>True if the user confirmed deletion.</summary>
    public bool Confirmed { get; private set; }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        EnableAcrylicBackdrop();
    }

    /// <summary>
    /// Enables the system Acrylic (blur-behind) backdrop via DWM on Windows 11.
    /// Falls back gracefully on older Windows versions.
    /// </summary>
    private void EnableAcrylicBackdrop()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        // DWMWA_SYSTEMBACKDROP_TYPE = 38, value 3 = Acrylic
        int backdropType = 3;
        DwmSetWindowAttribute(hwnd, 38, ref backdropType, sizeof(int));
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
