namespace JustRDP.Application.DTOs;

public class ConnectionDto : TreeEntryDto
{
    public string HostName { get; set; } = string.Empty;
    public int Port { get; set; } = 3389;
    public string? CredentialUsername { get; set; }
    public string? CredentialDomain { get; set; }
    public int DesktopWidth { get; set; }
    public int DesktopHeight { get; set; }
    public int ColorDepth { get; set; } = 32;
    public int ResizeBehavior { get; set; }
    public bool AutoReconnect { get; set; } = true;
    public bool NetworkLevelAuthentication { get; set; } = true;
    public bool Compression { get; set; } = true;
    public bool RedirectClipboard { get; set; } = true;
    public bool RedirectPrinters { get; set; }
    public bool RedirectDrives { get; set; }
    public bool RedirectSmartCards { get; set; }
    public bool RedirectPorts { get; set; }
    public int AudioRedirectionMode { get; set; }
    public string? GatewayHostName { get; set; }
    public string? Notes { get; set; }
}
