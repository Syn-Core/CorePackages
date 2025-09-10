namespace Syn.Core.Cache.Redis.Interfaces;

public interface ISerializer
{
    byte[] Serialize<T>(T obj);
    T Deserialize<T>(byte[] data);
}
