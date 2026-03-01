using System.Text.Json;
using JustRDP.Domain.Entities;
using JustRDP.Domain.Enums;

namespace JustRDP.Infrastructure.Import;

public static class JsonTreeImporter
{
    public static List<TreeEntry> Import(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var nodes = JsonSerializer.Deserialize<List<JsonTreeNode>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];

        var entries = new List<TreeEntry>();
        foreach (var node in nodes)
        {
            ImportNode(node, null, entries);
        }
        return entries;
    }

    private static void ImportNode(JsonTreeNode node, Guid? parentId, List<TreeEntry> entries)
    {
        if (node.Type == "Folder")
        {
            var folder = new FolderEntry
            {
                Name = node.Name ?? "Unnamed Folder",
                ParentId = parentId,
                SortOrder = entries.Count(e => e.ParentId == parentId),
                CredentialUsername = node.CredentialUsername,
                CredentialDomain = node.CredentialDomain
            };
            entries.Add(folder);

            if (node.Children is not null)
            {
                foreach (var child in node.Children)
                {
                    ImportNode(child, folder.Id, entries);
                }
            }
        }
        else
        {
            var connType = Enum.TryParse<ConnectionType>(node.ConnectionType, true, out var ct) ? ct : ConnectionType.RDP;
            var connection = new ConnectionEntry
            {
                Name = node.Name ?? "Unnamed Connection",
                ParentId = parentId,
                SortOrder = entries.Count(e => e.ParentId == parentId),
                ConnectionType = connType,
                HostName = node.HostName ?? string.Empty,
                Port = node.Port ?? (connType == ConnectionType.SSH ? 22 : 3389),
                CredentialUsername = node.CredentialUsername,
                CredentialDomain = node.CredentialDomain,
                DesktopWidth = node.DesktopWidth ?? 0,
                DesktopHeight = node.DesktopHeight ?? 0,
                ColorDepth = node.ColorDepth ?? 32,
                ResizeBehavior = node.ResizeBehavior ?? 0,
                AutoReconnect = node.AutoReconnect ?? true,
                NetworkLevelAuthentication = node.NetworkLevelAuthentication ?? true,
                Compression = node.Compression ?? true,
                RedirectClipboard = node.RedirectClipboard ?? true,
                RedirectPrinters = node.RedirectPrinters ?? false,
                RedirectDrives = node.RedirectDrives ?? false,
                RedirectSmartCards = node.RedirectSmartCards ?? false,
                RedirectPorts = node.RedirectPorts ?? false,
                AudioRedirectionMode = node.AudioRedirectionMode ?? 0,
                GatewayHostName = node.GatewayHostName,
                Notes = node.Notes,
                SshPrivateKeyPath = node.SshPrivateKeyPath,
                SshTerminalFontFamily = node.SshTerminalFontFamily,
                SshTerminalFontSize = node.SshTerminalFontSize
            };
            entries.Add(connection);
        }
    }
}

internal class JsonTreeNode
{
    public string? Type { get; set; }
    public string? Name { get; set; }
    public string? ConnectionType { get; set; }
    public string? HostName { get; set; }
    public int? Port { get; set; }
    public string? CredentialUsername { get; set; }
    public string? CredentialDomain { get; set; }
    public int? DesktopWidth { get; set; }
    public int? DesktopHeight { get; set; }
    public int? ColorDepth { get; set; }
    public int? ResizeBehavior { get; set; }
    public bool? AutoReconnect { get; set; }
    public bool? NetworkLevelAuthentication { get; set; }
    public bool? Compression { get; set; }
    public bool? RedirectClipboard { get; set; }
    public bool? RedirectPrinters { get; set; }
    public bool? RedirectDrives { get; set; }
    public bool? RedirectSmartCards { get; set; }
    public bool? RedirectPorts { get; set; }
    public int? AudioRedirectionMode { get; set; }
    public string? GatewayHostName { get; set; }
    public string? Notes { get; set; }
    public string? SshPrivateKeyPath { get; set; }
    public string? SshTerminalFontFamily { get; set; }
    public double? SshTerminalFontSize { get; set; }
    public List<JsonTreeNode>? Children { get; set; }
}
