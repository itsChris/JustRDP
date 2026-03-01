using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using JustRDP.Presentation.ViewModels;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace JustRDP.Presentation.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
    }

    private void EditTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.Focus();
            textBox.SelectAll();
        }
    }

    private void EditTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: TreeEntryViewModel vm })
        {
            if (vm.IsEditing)
            {
                ViewModel.CommitRenameAsync(vm).ConfigureAwait(false);
            }
        }
    }

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        ViewModel.TreeVM.SelectedEntry = e.NewValue as TreeEntryViewModel;
    }

    private void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled) return;
        if (sender is not TreeViewItem tvi) return;

        DependencyObject? source = e.OriginalSource as DependencyObject;
        while (source is not null and not TreeViewItem)
            source = VisualTreeHelper.GetParent(source);
        if (source != tvi) return;

        if (tvi.DataContext is TreeEntryViewModel vm)
        {
            ViewModel.TreeVM.DoubleClickEntryCommand.Execute(vm);
            e.Handled = true;
        }
    }

    private void EditTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: TreeEntryViewModel vm }) return;

        switch (e.Key)
        {
            case Key.Enter:
                ViewModel.CommitRenameAsync(vm).ConfigureAwait(false);
                e.Handled = true;
                break;
            case Key.Escape:
                vm.CancelEditCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
