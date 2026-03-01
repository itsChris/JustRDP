using System.Security.Cryptography;
using System.Text;
using JustRDP.Domain.Interfaces;

namespace JustRDP.Infrastructure.Security;

public class DpapiCredentialEncryptor : ICredentialEncryptor
{
    public byte[] Encrypt(string plainText)
    {
        var bytes = Encoding.UTF8.GetBytes(plainText);
        return ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
    }

    public string Decrypt(byte[] cipherText)
    {
        var bytes = ProtectedData.Unprotect(cipherText, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}
