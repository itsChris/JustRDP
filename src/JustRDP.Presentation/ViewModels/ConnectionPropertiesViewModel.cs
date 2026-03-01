using CommunityToolkit.Mvvm.ComponentModel;
using JustRDP.Application.Services;
using JustRDP.Domain.Entities;
using JustRDP.Domain.Enums;
using JustRDP.Domain.Interfaces;

namespace JustRDP.Presentation.ViewModels;

public partial class ConnectionPropertiesViewModel : ObservableObject
{
    private readonly TreeService _treeService;
    private readonly ICredentialEncryptor _encryptor;
    private ConnectionEntry? _connection;
    private FolderEntry? _folder;

    // General
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _hostName = string.Empty;
    [ObservableProperty] private int _port = 3389;

    // Credentials
    [ObservableProperty] private string? _credentialUsername;
    [ObservableProperty] private string? _credentialDomain;
    [ObservableProperty] private string? _credentialPassword;

    // Display
    [ObservableProperty] private int _desktopWidth;
    [ObservableProperty] private int _desktopHeight;
    [ObservableProperty] private int _colorDepth = 32;
    [ObservableProperty] private int _resizeBehavior;

    // Connection
    [ObservableProperty] private bool _autoReconnect = true;
    [ObservableProperty] private bool _networkLevelAuthentication = true;
    [ObservableProperty] private bool _compression = true;

    // Redirection
    [ObservableProperty] private bool _redirectClipboard = true;
    [ObservableProperty] private bool _redirectPrinters;
    [ObservableProperty] private bool _redirectDrives;
    [ObservableProperty] private bool _redirectSmartCards;
    [ObservableProperty] private bool _redirectPorts;

    // Audio
    [ObservableProperty] private int _audioRedirectionMode;

    // Gateway
    [ObservableProperty] private string? _gatewayHostName;

    // Notes
    [ObservableProperty] private string? _notes;

    // Connection Type
    [ObservableProperty] private ConnectionType _connectionType;

    partial void OnConnectionTypeChanged(ConnectionType value)
    {
        OnPropertyChanged(nameof(IsSsh));
        OnPropertyChanged(nameof(IsRdp));
        // Auto-switch default port
        if (value == ConnectionType.SSH && Port == 3389)
            Port = 22;
        else if (value == ConnectionType.RDP && Port == 22)
            Port = 3389;
    }

    public bool IsSsh => !IsFolder && ConnectionType == ConnectionType.SSH;
    public bool IsRdp => !IsFolder && ConnectionType == ConnectionType.RDP;

    // SSH
    [ObservableProperty] private string? _sshPrivateKeyPath;
    [ObservableProperty] private string? _sshPrivateKeyPassphrase;
    [ObservableProperty] private string? _sshTerminalFontFamily;
    [ObservableProperty] private double? _sshTerminalFontSize;

    // UI
    [ObservableProperty] private bool _isFolder;
    [ObservableProperty] private string _windowTitle = "Properties";

    public ConnectionPropertiesViewModel(TreeService treeService, ICredentialEncryptor encryptor)
    {
        _treeService = treeService;
        _encryptor = encryptor;
    }

    public void LoadConnection(ConnectionEntry connection)
    {
        _connection = connection;
        _folder = null;
        IsFolder = false;
        WindowTitle = $"Properties - {connection.Name}";

        Name = connection.Name;
        HostName = connection.HostName;
        Port = connection.Port;
        CredentialUsername = connection.CredentialUsername;
        CredentialDomain = connection.CredentialDomain;
        CredentialPassword = connection.CredentialPasswordEncrypted is not null
            ? _encryptor.Decrypt(connection.CredentialPasswordEncrypted)
            : null;
        DesktopWidth = connection.DesktopWidth;
        DesktopHeight = connection.DesktopHeight;
        ColorDepth = connection.ColorDepth;
        ResizeBehavior = connection.ResizeBehavior;
        AutoReconnect = connection.AutoReconnect;
        NetworkLevelAuthentication = connection.NetworkLevelAuthentication;
        Compression = connection.Compression;
        RedirectClipboard = connection.RedirectClipboard;
        RedirectPrinters = connection.RedirectPrinters;
        RedirectDrives = connection.RedirectDrives;
        RedirectSmartCards = connection.RedirectSmartCards;
        RedirectPorts = connection.RedirectPorts;
        AudioRedirectionMode = connection.AudioRedirectionMode;
        GatewayHostName = connection.GatewayHostName;
        Notes = connection.Notes;
        ConnectionType = connection.ConnectionType;
        SshPrivateKeyPath = connection.SshPrivateKeyPath;
        SshPrivateKeyPassphrase = connection.SshPrivateKeyPassphraseEncrypted is not null
            ? _encryptor.Decrypt(connection.SshPrivateKeyPassphraseEncrypted)
            : null;
        SshTerminalFontFamily = connection.SshTerminalFontFamily;
        SshTerminalFontSize = connection.SshTerminalFontSize;
    }

    public void LoadFolder(FolderEntry folder)
    {
        _folder = folder;
        _connection = null;
        IsFolder = true;
        WindowTitle = $"Properties - {folder.Name}";

        Name = folder.Name;
        CredentialUsername = folder.CredentialUsername;
        CredentialDomain = folder.CredentialDomain;
        CredentialPassword = folder.CredentialPasswordEncrypted is not null
            ? _encryptor.Decrypt(folder.CredentialPasswordEncrypted)
            : null;
    }

    public async Task SaveAsync()
    {
        if (_connection is not null)
        {
            _connection.Name = Name;
            _connection.HostName = HostName;
            _connection.Port = Port;
            _connection.CredentialUsername = string.IsNullOrWhiteSpace(CredentialUsername) ? null : CredentialUsername;
            _connection.CredentialDomain = string.IsNullOrWhiteSpace(CredentialDomain) ? null : CredentialDomain;
            _connection.CredentialPasswordEncrypted = !string.IsNullOrEmpty(CredentialPassword)
                ? _encryptor.Encrypt(CredentialPassword)
                : null;
            _connection.DesktopWidth = DesktopWidth;
            _connection.DesktopHeight = DesktopHeight;
            _connection.ColorDepth = ColorDepth;
            _connection.ResizeBehavior = ResizeBehavior;
            _connection.AutoReconnect = AutoReconnect;
            _connection.NetworkLevelAuthentication = NetworkLevelAuthentication;
            _connection.Compression = Compression;
            _connection.RedirectClipboard = RedirectClipboard;
            _connection.RedirectPrinters = RedirectPrinters;
            _connection.RedirectDrives = RedirectDrives;
            _connection.RedirectSmartCards = RedirectSmartCards;
            _connection.RedirectPorts = RedirectPorts;
            _connection.AudioRedirectionMode = AudioRedirectionMode;
            _connection.GatewayHostName = string.IsNullOrWhiteSpace(GatewayHostName) ? null : GatewayHostName;
            _connection.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes;
            _connection.ConnectionType = ConnectionType;
            _connection.SshPrivateKeyPath = string.IsNullOrWhiteSpace(SshPrivateKeyPath) ? null : SshPrivateKeyPath;
            _connection.SshPrivateKeyPassphraseEncrypted = !string.IsNullOrEmpty(SshPrivateKeyPassphrase)
                ? _encryptor.Encrypt(SshPrivateKeyPassphrase)
                : null;
            _connection.SshTerminalFontFamily = string.IsNullOrWhiteSpace(SshTerminalFontFamily) ? null : SshTerminalFontFamily;
            _connection.SshTerminalFontSize = SshTerminalFontSize;

            await _treeService.UpdateAsync(_connection);
        }
        else if (_folder is not null)
        {
            _folder.Name = Name;
            _folder.CredentialUsername = string.IsNullOrWhiteSpace(CredentialUsername) ? null : CredentialUsername;
            _folder.CredentialDomain = string.IsNullOrWhiteSpace(CredentialDomain) ? null : CredentialDomain;
            _folder.CredentialPasswordEncrypted = !string.IsNullOrEmpty(CredentialPassword)
                ? _encryptor.Encrypt(CredentialPassword)
                : null;

            await _treeService.UpdateAsync(_folder);
        }
    }
}
