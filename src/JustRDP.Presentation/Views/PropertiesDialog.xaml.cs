using System.Windows;
using JustRDP.Presentation.ViewModels;

namespace JustRDP.Presentation.Views;

public partial class PropertiesDialog : Window
{
    private readonly ConnectionPropertiesViewModel _viewModel;

    public PropertiesDialog(ConnectionPropertiesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private async void OkButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SaveAsync();
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
