using JustRDP.Application.DTOs;
using JustRDP.Domain.Entities;
using JustRDP.Domain.Enums;

namespace JustRDP.Application.Mapping;

public static class TreeEntryMappingExtensions
{
    public static TreeEntryDto ToDto(this TreeEntry entry) => entry switch
    {
        ConnectionEntry c => c.ToConnectionDto(),
        FolderEntry f => f.ToFolderDto(),
        _ => throw new InvalidOperationException($"Unknown entry type: {entry.GetType().Name}")
    };

    public static ConnectionDto ToConnectionDto(this ConnectionEntry c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        ParentId = c.ParentId,
        SortOrder = c.SortOrder,
        EntryType = TreeEntryType.Connection,
        HostName = c.HostName,
        Port = c.Port,
        CredentialUsername = c.CredentialUsername,
        CredentialDomain = c.CredentialDomain,
        DesktopWidth = c.DesktopWidth,
        DesktopHeight = c.DesktopHeight,
        ColorDepth = c.ColorDepth,
        ResizeBehavior = c.ResizeBehavior,
        AutoReconnect = c.AutoReconnect,
        NetworkLevelAuthentication = c.NetworkLevelAuthentication,
        Compression = c.Compression,
        RedirectClipboard = c.RedirectClipboard,
        RedirectPrinters = c.RedirectPrinters,
        RedirectDrives = c.RedirectDrives,
        RedirectSmartCards = c.RedirectSmartCards,
        RedirectPorts = c.RedirectPorts,
        AudioRedirectionMode = c.AudioRedirectionMode,
        GatewayHostName = c.GatewayHostName,
        Notes = c.Notes
    };

    public static FolderDto ToFolderDto(this FolderEntry f) => new()
    {
        Id = f.Id,
        Name = f.Name,
        ParentId = f.ParentId,
        SortOrder = f.SortOrder,
        EntryType = TreeEntryType.Folder,
        IsExpanded = f.IsExpanded,
        CredentialUsername = f.CredentialUsername,
        CredentialDomain = f.CredentialDomain,
        HasCredentials = !string.IsNullOrEmpty(f.CredentialUsername),
        Children = f.Children.Select(c => c.ToDto()).ToList()
    };
}
