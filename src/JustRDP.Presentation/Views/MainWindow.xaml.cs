using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using JustRDP.Domain.Interfaces;
using JustRDP.Presentation.ViewModels;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace JustRDP.Presentation.Views;

public partial class MainWindow : Window
{
    private readonly ISettingsRepository _settings;
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    public MainWindow(MainWindowViewModel viewModel, ISettingsRepository settings)
    {
        InitializeComponent();
        DataContext = viewModel;
        _settings = settings;
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        await RestoreWindowStateAsync();
        await ViewModel.InitializeAsync();
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        await SaveWindowStateAsync();
    }

    private async Task SaveWindowStateAsync()
    {
        // Save the restore bounds (position/size when not maximized) so we can
        // restore properly even if the window is currently maximized.
        var bounds = WindowState == WindowState.Maximized ? RestoreBounds : new Rect(Left, Top, Width, Height);

        await _settings.SetAsync("Window.Left", bounds.Left.ToString(CultureInfo.InvariantCulture));
        await _settings.SetAsync("Window.Top", bounds.Top.ToString(CultureInfo.InvariantCulture));
        await _settings.SetAsync("Window.Width", bounds.Width.ToString(CultureInfo.InvariantCulture));
        await _settings.SetAsync("Window.Height", bounds.Height.ToString(CultureInfo.InvariantCulture));
        await _settings.SetAsync("Window.State", WindowState.ToString());
        await _settings.SetAsync("Window.TreeWidth", TreeColumn.Width.Value.ToString(CultureInfo.InvariantCulture));
    }

    private async Task RestoreWindowStateAsync()
    {
        var left = await _settings.GetAsync("Window.Left");
        var top = await _settings.GetAsync("Window.Top");
        var width = await _settings.GetAsync("Window.Width");
        var height = await _settings.GetAsync("Window.Height");
        var state = await _settings.GetAsync("Window.State");

        if (double.TryParse(left, CultureInfo.InvariantCulture, out var l) &&
            double.TryParse(top, CultureInfo.InvariantCulture, out var t) &&
            double.TryParse(width, CultureInfo.InvariantCulture, out var w) &&
            double.TryParse(height, CultureInfo.InvariantCulture, out var h))
        {
            // Verify the saved position is still on a visible screen
            var rect = new Rect(l, t, w, h);
            if (IsRectOnAnyScreen(rect))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = l;
                Top = t;
                Width = w;
                Height = h;
            }
        }

        if (Enum.TryParse<WindowState>(state, out var ws) && ws != WindowState.Minimized)
        {
            WindowState = ws;
        }

        var treeWidth = await _settings.GetAsync("Window.TreeWidth");

        if (double.TryParse(treeWidth, CultureInfo.InvariantCulture, out var tw) && tw >= 150)
            TreeColumn.Width = new GridLength(tw);
    }

    private static bool IsRectOnAnyScreen(Rect rect)
    {
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var workArea = screen.WorkingArea;
            // Check if at least part of the window is visible on this screen
            if (rect.Left < workArea.Right && rect.Right > workArea.Left &&
                rect.Top < workArea.Bottom && rect.Bottom > workArea.Top)
                return true;
        }
        return false;
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

    private void QuickConnectBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ViewModel.QuickConnectCommand.CanExecute(null))
        {
            ViewModel.QuickConnectCommand.Execute(null);
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
