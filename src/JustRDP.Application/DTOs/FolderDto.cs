namespace JustRDP.Application.DTOs;

public class FolderDto : TreeEntryDto
{
    public bool IsExpanded { get; set; }
    public string? CredentialUsername { get; set; }
    public string? CredentialDomain { get; set; }
    public bool HasCredentials { get; set; }
    public List<TreeEntryDto> Children { get; set; } = [];
}
