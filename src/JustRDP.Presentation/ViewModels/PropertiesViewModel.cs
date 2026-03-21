using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JustRDP.Domain.Entities;
using JustRDP.Domain.Enums;
using JustRDP.Domain.ValueObjects;

namespace JustRDP.Presentation.ViewModels;

public partial class PropertiesViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private TreeEntryType _entryType;

    [ObservableProperty]
    private string _hostName = string.Empty;

    [ObservableProperty]
    private int _port;

    [ObservableProperty]
    private string _credentialDisplay = "None";

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private bool _hasEntry;

    [ObservableProperty]
    private bool _isConnection;

    [ObservableProperty]
    private DateTime? _lastConnected;

    [ObservableProperty]
    private int _timesConnected;

    private TreeEntry? _currentEntry;

    public void LoadEntry(TreeEntry? entry, Credential? resolvedCredential = null)
    {
        _currentEntry = entry;
        HasEntry = entry is not null;

        if (entry is null)
        {
            Name = string.Empty;
            HostName = string.Empty;
            Port = 0;
            CredentialDisplay = "None";
            Notes = null;
            IsConnection = false;
            LastConnected = null;
            TimesConnected = 0;
            return;
        }

        Name = entry.Name;
        EntryType = entry is FolderEntry ? TreeEntryType.Folder : TreeEntryType.Connection;

        if (entry is ConnectionEntry conn)
        {
            IsConnection = true;
            HostName = conn.HostName;
            Port = conn.Port;
            Notes = conn.Notes;
            LastConnected = conn.LastConnectedAt;
            TimesConnected = conn.ConnectCount;

            if (resolvedCredential is not null && !resolvedCredential.IsEmpty)
            {
                var display = resolvedCredential.Username ?? "";
                if (!string.IsNullOrEmpty(resolvedCredential.Domain))
                    display = $"{resolvedCredential.Domain}\\{display}";
                if (resolvedCredential.IsInherited)
                    display += $" (from: {resolvedCredential.InheritedFromName})";
                CredentialDisplay = display;
            }
            else
            {
                CredentialDisplay = "None";
            }
        }
        else
        {
            IsConnection = false;
            HostName = string.Empty;
            Port = 0;
            Notes = null;
            LastConnected = null;
            TimesConnected = 0;

            if (entry is FolderEntry folder && !string.IsNullOrEmpty(folder.CredentialUsername))
            {
                var display = folder.CredentialUsername;
                if (!string.IsNullOrEmpty(folder.CredentialDomain))
                    display = $"{folder.CredentialDomain}\\{display}";
                CredentialDisplay = display;
            }
            else
            {
                CredentialDisplay = "None";
            }
        }
    }
}
