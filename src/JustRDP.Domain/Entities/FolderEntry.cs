using System.Collections.ObjectModel;

namespace JustRDP.Domain.Entities;

public class FolderEntry : TreeEntry
{
    public bool IsExpanded { get; set; }

    // Credential fields for inheritance
    public string? CredentialUsername { get; set; }
    public string? CredentialDomain { get; set; }
    public byte[]? CredentialPasswordEncrypted { get; set; }

    public ObservableCollection<TreeEntry> Children { get; set; } = [];
}
