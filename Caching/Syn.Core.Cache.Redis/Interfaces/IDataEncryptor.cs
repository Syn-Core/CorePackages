namespace Syn.Core.Cache.Redis.Interfaces;

public interface IDataEncryptor
{
    byte[] Encrypt(byte[] data);
    byte[] Decrypt(byte[] encryptedData);
}
