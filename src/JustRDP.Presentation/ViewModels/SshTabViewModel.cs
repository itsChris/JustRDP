using CommunityToolkit.Mvvm.ComponentModel;
using JustRDP.Domain.Entities;
using JustRDP.Domain.Interfaces;
using JustRDP.Domain.ValueObjects;
using JustRDP.Presentation.Controls.Terminal;
using Microsoft.Extensions.Logging;

namespace JustRDP.Presentation.ViewModels;

public partial class SshTabViewModel : ObservableObject, IConnectionTab
{
    private readonly ConnectionEntry _connection;
    private readonly Credential _credential;
    private readonly ICredentialEncryptor _encryptor;
    private readonly ILogger<SshTabViewModel>? _logger;

    [ObservableProperty]
    private string _tabTitle;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _statusMessage = "Connecting...";

    [ObservableProperty]
    private bool _isConnecting = true;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string? _errorMessage;

    public Guid ConnectionId => _connection.Id;
    public ConnectionEntry ConnectionEntry => _connection;
    public ILoggerFactory? LoggerFactory { get; }

    public event Action<IConnectionTab>? CloseRequested;

    public SshTabViewModel(ConnectionEntry connection, Credential credential, ICredentialEncryptor encryptor, ILoggerFactory? loggerFactory = null)
    {
        _connection = connection;
        _credential = credential;
        _encryptor = encryptor;
        TabTitle = connection.Name;
        LoggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<SshTabViewModel>();

        _logger?.LogDebug("[SshTabVM] Created for '{Name}' ({Host}:{Port}), ConnectionId={Id}, " +
            "hasUser={HasUser}, hasPassword={HasPass}, hasKey={HasKey}",
            connection.Name, connection.HostName, connection.Port, connection.Id,
            !string.IsNullOrWhiteSpace(credential.Username),
            !string.IsNullOrEmpty(credential.Password),
            !string.IsNullOrEmpty(connection.SshPrivateKeyPath));
    }

    public TerminalOptions BuildTerminalOptions()
    {
        _logger?.LogDebug("[SshTabVM] BuildTerminalOptions for '{Name}': user={User}, hasPassword={HasPass}, " +
            "keyPath={KeyPath}, font={Font}@{FontSize}",
            _connection.Name,
            string.IsNullOrWhiteSpace(_credential.Username) ? "<none>" : _credential.Username,
            !string.IsNullOrEmpty(_credential.Password),
            _connection.SshPrivateKeyPath ?? "<none>",
            _connection.SshTerminalFontFamily ?? "Consolas",
            _connection.SshTerminalFontSize ?? 14);

        string? passphrase = null;
        if (_connection.SshPrivateKeyPassphraseEncrypted is not null)
        {
            passphrase = _encryptor.Decrypt(_connection.SshPrivateKeyPassphraseEncrypted);
            _logger?.LogDebug("[SshTabVM] Decrypted private key passphrase (length={Length})", passphrase?.Length ?? 0);
        }

        return new TerminalOptions
        {
            Host = _connection.HostName,
            Port = _connection.Port,
            Username = _credential.Username,
            Password = _credential.Password,
            PrivateKeyPath = _connection.SshPrivateKeyPath,
            PrivateKeyPassphrase = passphrase,
            FontFamily = _connection.SshTerminalFontFamily ?? "Consolas",
            FontSize = _connection.SshTerminalFontSize ?? 14
        };
    }

    public void OnConnected()
    {
        _logger?.LogInformation("[SshTabVM] OnConnected for '{Name}'", TabTitle);
        IsConnecting = false;
        HasError = false;
        StatusMessage = "Connected";
    }

    public void OnDisconnected()
    {
        _logger?.LogInformation("[SshTabVM] OnDisconnected for '{Name}', firing CloseRequested", TabTitle);
        CloseRequested?.Invoke(this);
    }

    public void OnError(Exception ex)
    {
        _logger?.LogError(ex, "[SshTabVM] OnError for '{Name}'", TabTitle);
        IsConnecting = false;
        HasError = true;
        ErrorMessage = ex.Message;
        StatusMessage = $"Error: {ex.Message}";
    }

    public void Disconnect()
    {
        _logger?.LogDebug("[SshTabVM] Disconnect called for '{Name}'", TabTitle);
        // Actual disconnect handled by the View (SshTabView) which owns the control
    }
}
