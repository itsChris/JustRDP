using System.Windows;
using System.Windows.Threading;
using JustRDP.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace JustRDP.Presentation.Views;

public partial class SshTabView : System.Windows.Controls.UserControl
{
    private bool _connected;

    public SshTabView()
    {
        InitializeComponent();
    }

    private void SshTabView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_connected) return;
        if (DataContext is not SshTabViewModel vm) return;

        _connected = true;

        // Provide ILoggerFactory to the terminal control
        if (vm.LoggerFactory is not null)
            TerminalControl.SetLoggerFactory(vm.LoggerFactory);

        TerminalControl.SessionConnected += vm.OnConnected;
        TerminalControl.SessionDisconnected += vm.OnDisconnected;
        TerminalControl.SessionError += vm.OnError;

        // Defer connect so the control has time to size
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, async () =>
        {
            try
            {
                var options = vm.BuildTerminalOptions();
                await TerminalControl.ConnectAsync(options);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[SSH] ConnectAsync failed for {Name}", vm.TabTitle);
                vm.OnError(ex);
            }
        });
    }

    private void SshTabView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SshTabViewModel vm)
        {
            TerminalControl.SessionConnected -= vm.OnConnected;
            TerminalControl.SessionDisconnected -= vm.OnDisconnected;
            TerminalControl.SessionError -= vm.OnError;
        }

        TerminalControl.Disconnect();
    }
}
