namespace JustRDP.Domain.Interfaces;

public interface ICredentialEncryptor
{
    byte[] Encrypt(string plainText);
    string Decrypt(byte[] cipherText);
}
