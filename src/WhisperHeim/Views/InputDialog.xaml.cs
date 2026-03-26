using System.Windows;
using System.Windows.Input;

namespace WhisperHeim.Views;

public partial class InputDialog : Window
{
    public InputDialog(string title, string prompt, string defaultValue = "")
    {
        InitializeComponent();
        TitleText.Text = title;
        PromptText.Text = prompt;
        InputTextBox.Text = defaultValue;

        Loaded += (_, _) =>
        {
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        };
    }

    /// <summary>True if the user confirmed the input.</summary>
    public bool Confirmed { get; private set; }

    /// <summary>The text entered by the user.</summary>
    public string InputText => InputTextBox.Text?.Trim() ?? string.Empty;

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }

    private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Confirmed = true;
            Close();
        }
        else if (e.Key == Key.Escape)
        {
            Confirmed = false;
            Close();
        }
    }
}
