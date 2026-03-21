using JustRDP.Domain.Entities;
using JustRDP.Domain.Enums;
using JustRDP.Domain.Interfaces;

namespace JustRDP.Application.Services;

public class TreeService
{
    private readonly ITreeEntryRepository _repository;

    public TreeService(ITreeEntryRepository repository)
    {
        _repository = repository;
    }

    public Task<List<TreeEntry>> GetAllEntriesAsync() => _repository.GetAllAsync();

    public Task<TreeEntry?> GetEntryByIdAsync(Guid id) => _repository.GetByIdAsync(id);

    public Task<List<TreeEntry>> GetChildrenAsync(Guid? parentId) => _repository.GetChildrenAsync(parentId);

    public async Task<FolderEntry> CreateFolderAsync(string name, Guid? parentId = null)
    {
        var sortOrder = await _repository.GetNextSortOrderAsync(parentId);
        var folder = new FolderEntry
        {
            Name = name,
            ParentId = parentId,
            SortOrder = sortOrder
        };
        await _repository.AddAsync(folder);
        return folder;
    }

    public async Task<ConnectionEntry> CreateConnectionAsync(
        string name, string hostName, Guid? parentId = null,
        int port = 3389, ConnectionType connectionType = ConnectionType.RDP)
    {
        var sortOrder = await _repository.GetNextSortOrderAsync(parentId);
        var connection = new ConnectionEntry
        {
            Name = name,
            HostName = hostName,
            Port = port,
            ParentId = parentId,
            SortOrder = sortOrder,
            ConnectionType = connectionType
        };
        await _repository.AddAsync(connection);
        return connection;
    }

    public async Task RenameAsync(Guid id, string newName)
    {
        var entry = await _repository.GetByIdAsync(id);
        if (entry is null) return;
        entry.Name = newName;
        entry.ModifiedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entry);
    }

    public Task DeleteAsync(Guid id) => _repository.DeleteAsync(id);

    public Task UpdateAsync(TreeEntry entry)
    {
        entry.ModifiedAt = DateTime.UtcNow;
        return _repository.UpdateAsync(entry);
    }

    public Task UpdateSortOrderAsync(IEnumerable<(Guid Id, int SortOrder)> updates)
        => _repository.UpdateSortOrderAsync(updates);

    public Task UpdateUsageAsync(Guid connectionId, DateTime lastConnectedAt, int connectCount)
        => _repository.UpdateUsageAsync(connectionId, lastConnectedAt, connectCount);

    public async Task<ConnectionEntry> DuplicateConnectionAsync(ConnectionEntry source)
    {
        var sortOrder = await _repository.GetNextSortOrderAsync(source.ParentId);
        var duplicate = new ConnectionEntry
        {
            Name = source.Name + " (Copy)",
            ParentId = source.ParentId,
            SortOrder = sortOrder,
            HostName = source.HostName,
            Port = source.Port,
            CredentialUsername = source.CredentialUsername,
            CredentialDomain = source.CredentialDomain,
            CredentialPasswordEncrypted = source.CredentialPasswordEncrypted is not null
                ? (byte[])source.CredentialPasswordEncrypted.Clone()
                : null,
            DesktopWidth = source.DesktopWidth,
            DesktopHeight = source.DesktopHeight,
            ColorDepth = source.ColorDepth,
            ResizeBehavior = source.ResizeBehavior,
            AutoReconnect = source.AutoReconnect,
            NetworkLevelAuthentication = source.NetworkLevelAuthentication,
            Compression = source.Compression,
            RedirectClipboard = source.RedirectClipboard,
            RedirectPrinters = source.RedirectPrinters,
            RedirectDrives = source.RedirectDrives,
            RedirectSmartCards = source.RedirectSmartCards,
            RedirectPorts = source.RedirectPorts,
            AudioRedirectionMode = source.AudioRedirectionMode,
            GatewayHostName = source.GatewayHostName,
            GatewayUsageMethod = source.GatewayUsageMethod,
            GatewayUsername = source.GatewayUsername,
            GatewayDomain = source.GatewayDomain,
            GatewayPasswordEncrypted = source.GatewayPasswordEncrypted is not null
                ? (byte[])source.GatewayPasswordEncrypted.Clone()
                : null,
            Notes = source.Notes,
            ConnectionType = source.ConnectionType,
            SshPrivateKeyPath = source.SshPrivateKeyPath,
            SshPrivateKeyPassphraseEncrypted = source.SshPrivateKeyPassphraseEncrypted is not null
                ? (byte[])source.SshPrivateKeyPassphraseEncrypted.Clone()
                : null,
            SshTerminalFontFamily = source.SshTerminalFontFamily,
            SshTerminalFontSize = source.SshTerminalFontSize
        };
        await _repository.AddAsync(duplicate);
        return duplicate;
    }

    public Task<List<FolderEntry>> GetAncestorsAsync(Guid entryId)
        => _repository.GetAncestorsAsync(entryId);
}
