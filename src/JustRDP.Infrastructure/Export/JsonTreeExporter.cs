using System.Text.Json;
using System.Text.Json.Serialization;
using JustRDP.Domain.Entities;

namespace JustRDP.Infrastructure.Export;

public static class JsonTreeExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void Export(List<TreeEntry> allEntries, string filePath)
    {
        var rootEntries = allEntries.Where(e => e.ParentId is null).OrderBy(e => e.SortOrder);
        var nodes = rootEntries.Select(e => ToNode(e, allEntries)).ToList();
        var json = JsonSerializer.Serialize(nodes, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    private static object ToNode(TreeEntry entry, List<TreeEntry> allEntries)
    {
        if (entry is FolderEntry folder)
        {
            var children = allEntries
                .Where(e => e.ParentId == folder.Id)
                .OrderBy(e => e.SortOrder)
                .Select(e => ToNode(e, allEntries))
                .ToList();

            return new
            {
                Type = "Folder",
                folder.Name,
                CredentialUsername = folder.CredentialUsername,
                CredentialDomain = folder.CredentialDomain,
                Children = children.Count > 0 ? children : null
            };
        }

        var conn = (ConnectionEntry)entry;
        return new
        {
            Type = "Connection",
            conn.Name,
            conn.HostName,
            conn.Port,
            CredentialUsername = conn.CredentialUsername,
            CredentialDomain = conn.CredentialDomain,
            conn.DesktopWidth,
            conn.DesktopHeight,
            conn.ColorDepth,
            conn.ResizeBehavior,
            conn.AutoReconnect,
            conn.NetworkLevelAuthentication,
            conn.Compression,
            conn.RedirectClipboard,
            conn.RedirectPrinters,
            conn.RedirectDrives,
            conn.RedirectSmartCards,
            conn.RedirectPorts,
            conn.AudioRedirectionMode,
            GatewayHostName = conn.GatewayHostName,
            Notes = conn.Notes
        };
    }
}
