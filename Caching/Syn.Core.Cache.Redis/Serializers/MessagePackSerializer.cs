using Syn.Core.Cache.Redis.Interfaces;

namespace Syn.Core.Cache.Redis.Serializers;

internal class MessagePackSerializer : ISerializer
{
    public byte[] Serialize<T>(T obj)
    {
        return MessagePack.MessagePackSerializer.Serialize(obj);
    }

    public T Deserialize<T>(byte[] data)
    {
        return MessagePack.MessagePackSerializer.Deserialize<T>(data);
    }
}
