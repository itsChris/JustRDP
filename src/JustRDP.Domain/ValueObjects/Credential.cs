namespace JustRDP.Domain.ValueObjects;

public record Credential(
    string? Username,
    string? Domain,
    string? Password,
    string? InheritedFromName = null)
{
    public bool IsEmpty => string.IsNullOrEmpty(Username) && string.IsNullOrEmpty(Password);
    public bool IsInherited => InheritedFromName is not null;
}
