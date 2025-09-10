namespace Syn.Core.Cache.Redis.Interfaces;

public interface IDataCompressor
{
    byte[] Compress(byte[] data);
    byte[] Decompress(byte[] compressedData);
}
