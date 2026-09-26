using System.Windows;

namespace SqlUI.App.Views;

public partial class PromptDialog : Window
{
    public string Value => InputBox.Text;

    public PromptDialog(string title, string label, string defaultValue = "")
    {
        InitializeComponent();
        Title = title;
        LabelText.Text = label;
        InputBox.Text = defaultValue;
        InputBox.SelectAll();
        Loaded += (_, _) => InputBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    // Sahibinin (owner) uzerinde ortalanmis, sonucu string? olarak donen kisa yol.
    public static string? Show(Window owner, string title, string label, string defaultValue = "")
    {
        var dlg = new PromptDialog(title, label, defaultValue) { Owner = owner };
        return dlg.ShowDialog() == true ? dlg.Value : null;
    }
}
