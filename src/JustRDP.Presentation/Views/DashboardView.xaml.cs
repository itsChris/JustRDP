using JustRDP.Presentation.ViewModels;
using DataGrid = System.Windows.Controls.DataGrid;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using UserControl = System.Windows.Controls.UserControl;

namespace JustRDP.Presentation.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid { SelectedItem: DashboardConnectionItem item } &&
            DataContext is DashboardViewModel vm)
        {
            vm.ConnectFromDashboardCommand.Execute(item);
        }
    }
}
