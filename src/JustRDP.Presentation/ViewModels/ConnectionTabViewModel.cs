using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JustRDP.Domain.Entities;
using JustRDP.Domain.ValueObjects;
using RoyalApps.Community.Rdp.WinForms.Configuration;
using RoyalApps.Community.Rdp.WinForms.Controls;
using Serilog;

namespace JustRDP.Presentation.ViewModels;

public partial class ConnectionTabViewModel : ObservableObject, IConnectionTab
{
    private readonly ConnectionEntry _connection;
    private readonly Credential _credential;
    private RdpControl? _rdpControl;
    private Dispatcher? _dispatcher;

    [ObservableProperty]
    private string _tabTitle;

    [ObservableProperty]
    private string _statusMessage = "Connecting...";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isConnecting = true;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isSelected;

    public Guid ConnectionId => _connection.Id;

    public event Action<IConnectionTab>? CloseRequested;

    public ConnectionTabViewModel(ConnectionEntry connection, Credential credential)
    {
        _connection = connection;
        _credential = credential;
        _tabTitle = connection.Name;
        Log.Debug("ConnectionTabViewModel created for {Name} ({Host}:{Port})",
            connection.Name, connection.HostName, connection.Port);
    }

    public void ConfigureAndConnect(RdpControl rdpControl, Dispatcher dispatcher, int containerPixelWidth = 0, int containerPixelHeight = 0)
    {
        _dispatcher = dispatcher;
        Log.Debug("[RDP] ConfigureAndConnect called on thread {ThreadId}", Environment.CurrentManagedThreadId);

        if (string.IsNullOrWhiteSpace(_connection.HostName))
        {
            IsConnecting = false;
            HasError = true;
            ErrorMessage = "Cannot connect: hostname is not configured. Edit the connection properties first.";
            StatusMessage = "Not connected";
            return;
        }

        _rdpControl = rdpControl;

        var config = rdpControl.RdpConfiguration;

        // Enable built-in RDP control logging
        var logDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JustRDP", "logs");
        System.IO.Directory.CreateDirectory(logDir);
        config.LogEnabled = true;
        config.LogLevel = "DEBUG";
        config.LogFilePath = System.IO.Path.Combine(logDir, $"rdp-{_connection.HostName}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        Log.Information("[RDP] Built-in RDP logging enabled at {LogFile}", config.LogFilePath);

        // Server
        config.Server = _connection.HostName;
        config.Port = _connection.Port;
        Log.Debug("[RDP] Server={Server}, Port={Port}", config.Server, config.Port);

        // Credentials
        if (!_credential.IsEmpty)
        {
            config.Credentials.Username = _credential.Username ?? string.Empty;
            config.Credentials.Domain = _credential.Domain ?? string.Empty;
            config.Credentials.Password = new SensitiveString(_credential.Password ?? string.Empty);
            Log.Debug("[RDP] Credentials set: User={User}, Domain={Domain}, PasswordLength={PwdLen}",
                config.Credentials.Username, config.Credentials.Domain,
                (_credential.Password ?? "").Length);
        }
        else
        {
            Log.Debug("[RDP] No credentials configured (IsEmpty=true)");
        }
        config.Credentials.NetworkLevelAuthentication = _connection.NetworkLevelAuthentication;
        Log.Debug("[RDP] NLA={NLA}", config.Credentials.NetworkLevelAuthentication);

        // Display
        var desktopWidth = _connection.DesktopWidth > 0 ? _connection.DesktopWidth : containerPixelWidth;
        var desktopHeight = _connection.DesktopHeight > 0 ? _connection.DesktopHeight : containerPixelHeight;
        config.Display.DesktopWidth = desktopWidth;
        config.Display.DesktopHeight = desktopHeight;
        config.Display.ColorDepth = (RoyalApps.Community.Rdp.WinForms.Configuration.ColorDepth)_connection.ColorDepth;
        config.Display.AutoScaling = true;

        config.Display.ResizeBehavior = _connection.ResizeBehavior switch
        {
            1 => ResizeBehavior.SmartSizing,
            2 => ResizeBehavior.Scrollbars,
            _ => ResizeBehavior.SmartReconnect
        };

        Log.Information("[RDP] Display: DesktopSize={W}x{H} (container={CW}x{CH}), ColorDepth={CD}, AutoScaling={AS}, Resize={R}",
            desktopWidth, desktopHeight, containerPixelWidth, containerPixelHeight,
            config.Display.ColorDepth, config.Display.AutoScaling, config.Display.ResizeBehavior);

        // Redirection
        config.Redirection.RedirectClipboard = _connection.RedirectClipboard;
        config.Redirection.RedirectPrinters = _connection.RedirectPrinters;
        config.Redirection.RedirectDrives = _connection.RedirectDrives;
        config.Redirection.RedirectSmartCards = _connection.RedirectSmartCards;
        config.Redirection.RedirectPorts = _connection.RedirectPorts;
        config.Redirection.AudioRedirectionMode = (AudioRedirectionMode)_connection.AudioRedirectionMode;

        // Input — forward keyboard shortcuts (ALT combos, Windows key) to the remote session
        config.Input.KeyboardHookMode = true;
        config.Input.AcceleratorPassthrough = true;
        config.Input.EnableWindowsKey = true;

        // Connection
        config.Connection.EnableAutoReconnect = _connection.AutoReconnect;
        config.Connection.Compression = _connection.Compression;
        Log.Debug("[RDP] Connection: AutoReconnect={AR}, Compression={C}",
            config.Connection.EnableAutoReconnect, config.Connection.Compression);

        // Gateway
        if (!string.IsNullOrEmpty(_connection.GatewayHostName))
        {
            config.Gateway.GatewayHostname = _connection.GatewayHostName;
            config.Gateway.GatewayUsageMethod = GatewayUsageMethod.Always;
            Log.Debug("[RDP] Gateway: {GW}", config.Gateway.GatewayHostname);
        }

        // Subscribe to ALL events
        rdpControl.OnConnected += RdpControl_OnConnected;
        rdpControl.OnDisconnected += RdpControl_OnDisconnected;
        rdpControl.OnClientAreaClicked += (_, _) =>
            Log.Debug("[RDP] OnClientAreaClicked");
        rdpControl.OnConfirmClose += (_, _) =>
            Log.Debug("[RDP] OnConfirmClose");
        rdpControl.OnRequestContainerMinimize += (_, _) =>
            Log.Debug("[RDP] OnRequestContainerMinimize");
        rdpControl.OnRequestLeaveFullScreen += (_, _) =>
            Log.Debug("[RDP] OnRequestLeaveFullScreen");
        rdpControl.BeforeRemoteDesktopSizeChanged += (_, args) =>
            Log.Debug("[RDP] BeforeRemoteDesktopSizeChanged (Cancel={Cancel})", args.Cancel);
        rdpControl.RemoteDesktopSizeChanged += (_, _) =>
            Log.Debug("[RDP] RemoteDesktopSizeChanged");
        rdpControl.RdpClientConfigured += (_, _) =>
            Log.Debug("[RDP] RdpClientConfigured event fired (client created, about to connect)");

        Log.Information("[RDP] All events subscribed. Calling Connect() on thread {ThreadId}...",
            Environment.CurrentManagedThreadId);
        rdpControl.Connect();
        Log.Information("[RDP] Connect() returned. RdpControl.Size={W}x{H}, WasSuccessfullyConnected={WSC}",
            rdpControl.Width, rdpControl.Height, rdpControl.WasSuccessfullyConnected);
    }

    private void RdpControl_OnConnected(object? sender, ConnectedEventArgs args)
    {
        Log.Information("[RDP] >>> OnConnected fired on thread {ThreadId}. WasSuccessfullyConnected={WSC}",
            Environment.CurrentManagedThreadId, _rdpControl?.WasSuccessfullyConnected);

        _dispatcher?.InvokeAsync(() =>
        {
            Log.Debug("[RDP] OnConnected: Updating UI properties on thread {ThreadId}", Environment.CurrentManagedThreadId);
            IsConnecting = false;
            IsConnected = true;
            HasError = false;
            StatusMessage = "Connected";
            Log.Debug("[RDP] OnConnected: IsConnecting={IC}, IsConnected={ICd}, HasError={HE}",
                IsConnecting, IsConnected, HasError);

            // Focus the RDP client so it can receive input
            try
            {
                _rdpControl?.FocusRdpClient();
                Log.Debug("[RDP] FocusRdpClient() called successfully");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[RDP] FocusRdpClient() failed");
            }
        });
    }

    private void RdpControl_OnDisconnected(object? sender, DisconnectedEventArgs args)
    {
        Log.Information("[RDP] >>> OnDisconnected fired on thread {ThreadId}: Code={Code}, Desc={Desc}",
            Environment.CurrentManagedThreadId, args.DisconnectCode, args.Description);

        _dispatcher?.InvokeAsync(() =>
        {
            Log.Debug("[RDP] OnDisconnected: Updating UI properties on thread {ThreadId}", Environment.CurrentManagedThreadId);
            IsConnected = false;
            IsConnecting = false;
            StatusMessage = $"Disconnected (code: {args.DisconnectCode})";
            if (args.DisconnectCode != 0 && args.DisconnectCode != 1)
            {
                HasError = true;
                ErrorMessage = $"Disconnected with code {args.DisconnectCode}: {args.Description}";
            }

            // Close the tab when the user logs off / disconnects from the remote session
            Log.Debug("[RDP] Requesting tab close after disconnect (code {Code})", args.DisconnectCode);
            CloseRequested?.Invoke(this);
        });
    }

    [RelayCommand]
    private void Reconnect()
    {
        if (_rdpControl is null) return;
        Log.Information("[RDP] Reconnect requested");
        HasError = false;
        IsConnecting = true;
        StatusMessage = "Reconnecting...";
        _rdpControl.Connect();
    }

    public void Disconnect()
    {
        if (_rdpControl is null) return;
        Log.Information("[RDP] Disconnect called. IsConnected={IC}", IsConnected);

        try
        {
            if (IsConnected)
                _rdpControl.Disconnect();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[RDP] Disconnect threw exception");
        }

        _rdpControl.Dispose();
        _rdpControl = null;
        IsConnected = false;
    }
}
