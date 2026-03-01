using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using JustRDP.Presentation.ViewModels;
using RoyalApps.Community.Rdp.WinForms.Controls;
using Serilog;
using UserControl = System.Windows.Controls.UserControl;

namespace JustRDP.Presentation.Views;

public partial class ConnectionTabView : UserControl
{
    private bool _connected;

    public ConnectionTabView()
    {
        InitializeComponent();
        Log.Debug("[View] ConnectionTabView constructed");
    }

    private void ConnectionTabView_Loaded(object sender, RoutedEventArgs e)
    {
        Log.Debug("[View] ConnectionTabView_Loaded fired. _connected={Connected}, DataContext={DC}",
            _connected, DataContext?.GetType().Name ?? "null");

        if (_connected) return;
        if (DataContext is not ConnectionTabViewModel vm) return;

        _connected = true;

        Log.Debug("[View] Creating RdpControl...");
        var rdpControl = new RdpControl
        {
            Dock = System.Windows.Forms.DockStyle.Fill
        };
        Log.Debug("[View] RdpControl created. Size={W}x{H}", rdpControl.Width, rdpControl.Height);

        Log.Debug("[View] Setting WinFormsHost.Child...");
        WinFormsHost.Child = rdpControl;
        Log.Debug("[View] WinFormsHost.Child set. WFHost.ActualSize={W}x{H}, RdpControl.Size={RW}x{RH}",
            WinFormsHost.ActualWidth, WinFormsHost.ActualHeight,
            rdpControl.Width, rdpControl.Height);

        // Defer Connect() so the WindowsFormsHost has time to size the WinForms child.
        Log.Debug("[View] Scheduling deferred connect via Dispatcher.BeginInvoke(Loaded)...");
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            Log.Debug("[View] Deferred callback executing on thread {ThreadId}", Environment.CurrentManagedThreadId);

            var source = PresentationSource.FromVisual(this);
            var dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            var dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

            // Use the RdpControl's actual WinForms size (already in physical pixels)
            var pixelWidth = rdpControl.Width;
            var pixelHeight = rdpControl.Height;

            Log.Information(
                "[View] Deferred connect: WFHost.Actual={W}x{H}, DPI={DpiX}x{DpiY}, RdpControl.Size={RW}x{RH}",
                WinFormsHost.ActualWidth, WinFormsHost.ActualHeight,
                dpiX, dpiY, pixelWidth, pixelHeight);

            vm.ConfigureAndConnect(rdpControl, Dispatcher, pixelWidth, pixelHeight);

            Log.Debug("[View] ConfigureAndConnect returned. Scheduling UpdateClientSize...");

            // After connect, force the ActiveX to re-measure from its container
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                try
                {
                    Log.Debug("[View] Calling UpdateClientSize(). RdpControl.Size={W}x{H}, WasConnected={WSC}",
                        rdpControl.Width, rdpControl.Height, rdpControl.WasSuccessfullyConnected);
                    rdpControl.UpdateClientSize();
                    Log.Debug("[View] UpdateClientSize() completed");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[View] UpdateClientSize() failed");
                }
            });
        });
    }

    private void ConnectionTabView_Unloaded(object sender, RoutedEventArgs e)
    {
        Log.Debug("[View] ConnectionTabView_Unloaded fired");

        if (DataContext is ConnectionTabViewModel vm)
        {
            vm.Disconnect();
        }

        WinFormsHost.Child = null;
    }
}
