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
        if (_connected)
        {
            Log.Debug("[SshTabView] Loaded fired but already connected, skipping");
            return;
        }
        if (DataContext is not SshTabViewModel vm)
        {
            Log.Warning("[SshTabView] Loaded fired but DataContext is not SshTabViewModel (type={Type})",
                DataContext?.GetType().Name ?? "null");
            return;
        }

        _connected = true;
        Log.Debug("[SshTabView] Loaded for '{Name}', wiring events", vm.TabTitle);

        // Provide ILoggerFactory to the terminal control
        if (vm.LoggerFactory is not null)
            TerminalControl.SetLoggerFactory(vm.LoggerFactory);

        TerminalControl.SessionConnected += vm.OnConnected;
        TerminalControl.SessionDisconnected += vm.OnDisconnected;
        TerminalControl.SessionError += vm.OnError;
        TerminalControl.SessionInteractivePrompt += OnInteractivePrompt;
        Log.Debug("[SshTabView] Events wired (SessionConnected, SessionDisconnected, SessionError, SessionInteractivePrompt)");

        // Defer connect so the control has time to size
        Log.Debug("[SshTabView] Deferring ConnectAsync via Dispatcher.BeginInvoke(Loaded)");
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, async () =>
        {
            try
            {
                Log.Debug("[SshTabView] Dispatcher callback executing, building TerminalOptions");
                var options = vm.BuildTerminalOptions();
                Log.Debug("[SshTabView] Calling TerminalControl.ConnectAsync for {Host}:{Port}", options.Host, options.Port);
                await TerminalControl.ConnectAsync(options);
                Log.Debug("[SshTabView] TerminalControl.ConnectAsync completed successfully");
            }
            catch (OperationCanceledException)
            {
                Log.Information("[SshTabView] Connection cancelled for '{Name}'", vm.TabTitle);
                vm.OnDisconnected();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[SshTabView] ConnectAsync failed for '{Name}'", vm.TabTitle);
                vm.OnError(ex);
            }
        });
    }

    private void OnInteractivePrompt()
    {
        if (DataContext is SshTabViewModel vm)
        {
            Log.Debug("[SshTabView] Interactive prompt started, hiding connecting overlay");
            vm.IsConnecting = false;
        }
    }

    private void SshTabView_Unloaded(object sender, RoutedEventArgs e)
    {
        Log.Debug("[SshTabView] Unloaded fired");

        if (DataContext is SshTabViewModel vm)
        {
            Log.Debug("[SshTabView] Unwiring events for '{Name}'", vm.TabTitle);
            TerminalControl.SessionConnected -= vm.OnConnected;
            TerminalControl.SessionDisconnected -= vm.OnDisconnected;
            TerminalControl.SessionError -= vm.OnError;
            TerminalControl.SessionInteractivePrompt -= OnInteractivePrompt;
        }

        Log.Debug("[SshTabView] Calling TerminalControl.Disconnect()");
        TerminalControl.Disconnect();
        Log.Debug("[SshTabView] Unloaded complete");
    }
}
