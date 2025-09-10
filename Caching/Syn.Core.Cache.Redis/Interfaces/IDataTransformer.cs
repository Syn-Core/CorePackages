namespace Syn.Core.Cache.Redis.Interfaces;

public interface IDataTransformer
{
    byte[] Encode<T>(T obj);
    T Decode<T>(byte[] data);
}

