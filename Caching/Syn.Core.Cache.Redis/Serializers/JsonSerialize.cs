using Syn.Core.Cache.Redis.Interfaces;

using System.Text.Json;

namespace Syn.Core.Cache.Redis.Serializers;

internal class JsonSerialize : ISerializer
{
    private readonly JsonSerializerOptions _options;

    public JsonSerialize(JsonSerializerOptions options = null)
    {
        _options = options ?? new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    }

    public byte[] Serialize<T>(T obj)
    {
        return JsonSerializer.SerializeToUtf8Bytes(obj, _options);
    }

    public T Deserialize<T>(byte[] data)
    {
        return JsonSerializer.Deserialize<T>(data, _options)!;
    }
}