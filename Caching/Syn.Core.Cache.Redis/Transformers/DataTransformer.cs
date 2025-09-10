using Syn.Core.Cache.Redis.Interfaces;

namespace Syn.Core.Cache.Redis.Transformers;

internal class DataTransformer(ISerializer serializer, IDataCompressor compressor, IDataEncryptor encryptor) : IDataTransformer
{
    private readonly ISerializer _serializer = serializer;
    private readonly IDataCompressor _compressor = compressor;
    private readonly IDataEncryptor _encryptor = encryptor;

    public byte[] Encode<T>(T obj)
    {
        if (obj is null)
            throw new ArgumentNullException(nameof(obj), "Cannot encode a null object.");
        var serialized = _serializer.Serialize(obj);
        var compressed = _compressor.Compress(serialized);
        var encrypted = _encryptor.Encrypt(compressed);
        return encrypted;
    }

    public T Decode<T>(byte[] data)
    {
        if (data is null || data.Length == 0)
            throw new ArgumentNullException(nameof(data), "Cannot decode null or empty data.");
        var decrypted = _encryptor.Decrypt(data);
        var decompressed = _compressor.Decompress(decrypted);
        return _serializer.Deserialize<T>(decompressed);
    }
}

