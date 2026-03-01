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
        _tabTitle = connection.Name;
        LoggerFactory = loggerFactory;
    }

    public TerminalOptions BuildTerminalOptions()
    {
        string? passphrase = null;
        if (_connection.SshPrivateKeyPassphraseEncrypted is not null)
        {
            passphrase = _encryptor.Decrypt(_connection.SshPrivateKeyPassphraseEncrypted);
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
        IsConnecting = false;
        HasError = false;
        StatusMessage = "Connected";
    }

    public void OnDisconnected()
    {
        CloseRequested?.Invoke(this);
    }

    public void OnError(Exception ex)
    {
        IsConnecting = false;
        HasError = true;
        ErrorMessage = ex.Message;
        StatusMessage = $"Error: {ex.Message}";
    }

    public void Disconnect()
    {
        // Actual disconnect handled by the View (SshTabView) which owns the control
    }
}
