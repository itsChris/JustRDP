using JustRDP.Domain.Entities;
using JustRDP.Domain.Interfaces;
using JustRDP.Domain.ValueObjects;

namespace JustRDP.Application.Services;

public class CredentialInheritanceService
{
    private readonly ITreeEntryRepository _repository;
    private readonly ICredentialEncryptor _encryptor;

    public CredentialInheritanceService(ITreeEntryRepository repository, ICredentialEncryptor encryptor)
    {
        _repository = repository;
        _encryptor = encryptor;
    }

    public async Task<Credential> ResolveCredentialAsync(ConnectionEntry connection)
    {
        // 1. Check connection's own credentials
        if (!string.IsNullOrEmpty(connection.CredentialUsername))
        {
            var password = connection.CredentialPasswordEncrypted is not null
                ? _encryptor.Decrypt(connection.CredentialPasswordEncrypted)
                : null;
            return new Credential(connection.CredentialUsername, connection.CredentialDomain, password);
        }

        // 2. Walk ancestor folders
        var ancestors = await _repository.GetAncestorsAsync(connection.Id);
        foreach (var ancestor in ancestors)
        {
            if (!string.IsNullOrEmpty(ancestor.CredentialUsername))
            {
                var password = ancestor.CredentialPasswordEncrypted is not null
                    ? _encryptor.Decrypt(ancestor.CredentialPasswordEncrypted)
                    : null;
                return new Credential(
                    ancestor.CredentialUsername,
                    ancestor.CredentialDomain,
                    password,
                    ancestor.Name);
            }
        }

        // 3. No credentials found
        return new Credential(null, null, null);
    }
}
