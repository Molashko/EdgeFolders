using System.Windows;

namespace EdgeFolders.Windows;

public partial class InputDialog : Window
{
    public InputDialog(string title, string prompt, string value = "")
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        ValueBox.Text = value;
        Loaded += (_, _) =>
        {
            ValueBox.Focus();
            ValueBox.SelectAll();
        };
    }

    public string ResponseText => ValueBox.Text.Trim();

    public static string? Prompt(Window owner, string title, string prompt, string value = "")
    {
        var dialog = new InputDialog(title, prompt, value)
        {
            Owner = owner
        };

        return dialog.ShowDialog() == true ? dialog.ResponseText : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
