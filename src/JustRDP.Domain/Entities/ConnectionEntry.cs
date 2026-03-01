using JustRDP.Domain.Enums;

namespace JustRDP.Domain.Entities;

public class ConnectionEntry : TreeEntry
{
    // Connection
    public ConnectionType ConnectionType { get; set; } = ConnectionType.RDP;
    public string HostName { get; set; } = string.Empty;
    public int Port { get; set; } = 3389;

    // Credentials (optional, can inherit from folder)
    public string? CredentialUsername { get; set; }
    public string? CredentialDomain { get; set; }
    public byte[]? CredentialPasswordEncrypted { get; set; }

    // Display
    public int DesktopWidth { get; set; }
    public int DesktopHeight { get; set; }
    public int ColorDepth { get; set; } = 32;
    public int ResizeBehavior { get; set; } // 0=None, 1=SmartSizing, 2=SmartReconnect

    // Connection settings
    public bool AutoReconnect { get; set; } = true;
    public bool NetworkLevelAuthentication { get; set; } = true;
    public bool Compression { get; set; } = true;

    // Redirection
    public bool RedirectClipboard { get; set; } = true;
    public bool RedirectPrinters { get; set; }
    public bool RedirectDrives { get; set; }
    public bool RedirectSmartCards { get; set; }
    public bool RedirectPorts { get; set; }

    // Audio
    public int AudioRedirectionMode { get; set; } // 0=Local, 1=Remote, 2=None

    // Gateway (schema only, deferred implementation)
    public string? GatewayHostName { get; set; }
    public int GatewayUsageMethod { get; set; }
    public string? GatewayUsername { get; set; }
    public string? GatewayDomain { get; set; }
    public byte[]? GatewayPasswordEncrypted { get; set; }

    // Notes
    public string? Notes { get; set; }

    // SSH
    public string? SshPrivateKeyPath { get; set; }
    public byte[]? SshPrivateKeyPassphraseEncrypted { get; set; }
    public string? SshTerminalFontFamily { get; set; }
    public double? SshTerminalFontSize { get; set; }
}
