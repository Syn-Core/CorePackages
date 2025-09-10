using Syn.Core.Cache.Redis.Interfaces;

using System.Security.Cryptography;

namespace Syn.Core.Cache.Redis.Encryption;

internal class AesEncryptor : IDataEncryptor
{
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public AesEncryptor(string base64Key, string base64IV)
    {
        _key = Convert.FromBase64String(base64Key);
        _iv = Convert.FromBase64String(base64IV);
    }

    public byte[] Encrypt(byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;

        using var encryptor = aes.CreateEncryptor();
        return PerformCryptography(data, encryptor);
    }

    public byte[] Decrypt(byte[] encryptedData)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;

        using var decryptor = aes.CreateDecryptor();
        return PerformCryptography(encryptedData, decryptor);
    }

    private static byte[] PerformCryptography(byte[] data, ICryptoTransform transform)
    {
        using var ms = new MemoryStream();
        using var cryptoStream = new CryptoStream(ms, transform, CryptoStreamMode.Write);
        cryptoStream.Write(data, 0, data.Length);
        cryptoStream.FlushFinalBlock();
        return ms.ToArray();
    }
}
