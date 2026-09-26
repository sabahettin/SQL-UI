using System.Windows;
using System.Windows.Controls;
using SqlUI.App.ViewModels;

namespace SqlUI.App.Views;

public partial class ConnectionDialog : Window
{
    private readonly ConnectionDialogViewModel _viewModel;

    public ConnectionDialog(ConnectionDialogViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.RequestClose += result => { DialogResult = result; Close(); };
    }

    // PasswordBox.Password guvenlik nedeniyle bindable degildir; manuel senkronize edilir.
    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _viewModel.Password = ((PasswordBox)sender).Password;
}
