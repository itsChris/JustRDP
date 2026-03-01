using System.ComponentModel;
using System.Windows;
using JustRDP.Presentation.ViewModels;

namespace JustRDP.Presentation.Views;

public partial class NetworkScanWindow : Window
{
    private readonly NetworkScanViewModel _viewModel;

    public NetworkScanWindow(NetworkScanViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += async (_, _) => await viewModel.LoadFoldersAsync();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _viewModel.CancelIfRunning();
    }
}
