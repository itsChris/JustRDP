using System.Windows;
using JustRDP.Presentation.ViewModels;

namespace JustRDP.Presentation.Views;

public partial class ImportDialog : Window
{
    public ImportDialog(ImportDialogViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await viewModel.LoadFoldersAsync();
            await viewModel.LoadExistingHostsAsync();
        };
    }
}
