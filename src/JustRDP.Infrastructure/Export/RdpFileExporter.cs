using System.Text;
using JustRDP.Domain.Entities;

namespace JustRDP.Infrastructure.Export;

public static class RdpFileExporter
{
    public static void Export(ConnectionEntry connection, string filePath)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"full address:s:{connection.HostName}:{connection.Port}");
        sb.AppendLine($"server port:i:{connection.Port}");

        if (!string.IsNullOrEmpty(connection.CredentialUsername))
            sb.AppendLine($"username:s:{connection.CredentialUsername}");
        if (!string.IsNullOrEmpty(connection.CredentialDomain))
            sb.AppendLine($"domain:s:{connection.CredentialDomain}");

        if (connection.DesktopWidth > 0)
            sb.AppendLine($"desktopwidth:i:{connection.DesktopWidth}");
        if (connection.DesktopHeight > 0)
            sb.AppendLine($"desktopheight:i:{connection.DesktopHeight}");
        sb.AppendLine($"session bpp:i:{connection.ColorDepth}");

        if (connection.ResizeBehavior == 1)
            sb.AppendLine("smart sizing:i:1");

        sb.AppendLine($"enablecredsspsupport:i:{(connection.NetworkLevelAuthentication ? 1 : 0)}");
        sb.AppendLine($"compression:i:{(connection.Compression ? 1 : 0)}");
        sb.AppendLine($"redirectclipboard:i:{(connection.RedirectClipboard ? 1 : 0)}");
        sb.AppendLine($"redirectprinters:i:{(connection.RedirectPrinters ? 1 : 0)}");
        sb.AppendLine($"redirectdrives:i:{(connection.RedirectDrives ? 1 : 0)}");
        sb.AppendLine($"redirectsmartcards:i:{(connection.RedirectSmartCards ? 1 : 0)}");
        sb.AppendLine($"redirectcomports:i:{(connection.RedirectPorts ? 1 : 0)}");
        sb.AppendLine($"audiomode:i:{connection.AudioRedirectionMode}");
        sb.AppendLine($"autoreconnection enabled:i:{(connection.AutoReconnect ? 1 : 0)}");

        if (!string.IsNullOrEmpty(connection.GatewayHostName))
        {
            sb.AppendLine($"gatewayhostname:s:{connection.GatewayHostName}");
            sb.AppendLine($"gatewayusagemethod:i:{connection.GatewayUsageMethod}");
        }

        File.WriteAllText(filePath, sb.ToString());
    }
}
