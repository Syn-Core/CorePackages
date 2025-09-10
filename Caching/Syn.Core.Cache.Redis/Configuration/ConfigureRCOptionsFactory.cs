using Microsoft.Extensions.Options;

using Syn.Core.Cache.Redis.Interfaces;
using Syn.Core.Cache.Redis.Serializers;

namespace Syn.Core.Cache.Redis.Configuration;

public class ConfigureRCOptionsFactory
{
    private readonly IOptions<RCOptions> _options;

    public ConfigureRCOptionsFactory(IOptions<RCOptions> options)
    {
        options ??= Options.Create(new RCOptions
        {
            Serializer = "MessagePack", // Default serializer
            UseCompression = true // Default compression setting
        });
        _options = options;
    }

    /// <summary>
    /// How to use:
    /// <code>var serializer = SerializerFactory.Create{{MyModel}}();</code>
    /// <code>byte[] data = serializer.Serialize(myObject);</code>
    /// <code>MyModel restored = serializer.Deserialize(data);</code>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public ISerializer Create<T>()
    {
        return RCOptions.Instance.Serializer switch
        {
            "MessagePack" => new MessagePackSerializer(),
            "Json" => new JsonSerialize(),
            _ => throw new NotSupportedException($"Format {_options.Value.Serializer} not supported")
        };
    }

}
