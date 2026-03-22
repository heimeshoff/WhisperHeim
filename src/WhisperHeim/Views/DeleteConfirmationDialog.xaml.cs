using System.Windows;

namespace WhisperHeim.Views;

public partial class DeleteConfirmationDialog : Window
{
    public DeleteConfirmationDialog(string itemName, string title = "Delete Recording")
    {
        InitializeComponent();
        TitleText.Text = title;
        TranscriptNameText.Text = itemName;
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
}
