using JustRDP.Domain.Entities;

namespace JustRDP.Infrastructure.Import;

public static class RdpFileParser
{
    public static ConnectionEntry Parse(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        var connection = new ConnectionEntry
        {
            Name = Path.GetFileNameWithoutExtension(filePath)
        };

        foreach (var line in lines)
        {
            var parts = line.Split(':', 3);
            if (parts.Length < 3) continue;

            var key = parts[0].Trim().ToLowerInvariant();
            var type = parts[1].Trim();
            var value = parts[2].Trim();

            switch (key)
            {
                case "full address":
                    var addressParts = value.Split(':');
                    connection.HostName = addressParts[0];
                    if (addressParts.Length > 1 && int.TryParse(addressParts[1], out var port))
                        connection.Port = port;
                    break;
                case "server port":
                    if (int.TryParse(value, out var serverPort))
                        connection.Port = serverPort;
                    break;
                case "username":
                    connection.CredentialUsername = value;
                    break;
                case "domain":
                    connection.CredentialDomain = value;
                    break;
                case "desktopwidth":
                    if (int.TryParse(value, out var width))
                        connection.DesktopWidth = width;
                    break;
                case "desktopheight":
                    if (int.TryParse(value, out var height))
                        connection.DesktopHeight = height;
                    break;
                case "session bpp":
                    if (int.TryParse(value, out var bpp))
                        connection.ColorDepth = bpp;
                    break;
                case "smart sizing":
                    if (value == "1")
                        connection.ResizeBehavior = 1;
                    break;
                case "authentication level":
                    connection.NetworkLevelAuthentication = value != "0";
                    break;
                case "enablecredsspsupport":
                    connection.NetworkLevelAuthentication = value == "1";
                    break;
                case "compression":
                    connection.Compression = value == "1";
                    break;
                case "redirectclipboard":
                    connection.RedirectClipboard = value == "1";
                    break;
                case "redirectprinters":
                    connection.RedirectPrinters = value == "1";
                    break;
                case "redirectdrives":
                    connection.RedirectDrives = value == "1";
                    break;
                case "redirectsmartcards":
                    connection.RedirectSmartCards = value == "1";
                    break;
                case "redirectcomports":
                    connection.RedirectPorts = value == "1";
                    break;
                case "audiomode":
                    if (int.TryParse(value, out var audioMode))
                        connection.AudioRedirectionMode = audioMode;
                    break;
                case "gatewayhostname":
                    connection.GatewayHostName = value;
                    break;
                case "gatewayusagemethod":
                    if (int.TryParse(value, out var gwUsage))
                        connection.GatewayUsageMethod = gwUsage;
                    break;
                case "autoreconnection enabled":
                    connection.AutoReconnect = value == "1";
                    break;
            }
        }

        return connection;
    }
}
