
namespace Syn.Core.Cache.Redis.Configuration;

public class RCOptions
{
    public static readonly RCOptions Instance = new();

    /// <summary>
    /// MessagePack or Json, default is MessagePack.
    /// </summary>
    public string Serializer { get; set; } = "MessagePack";
    public bool UseCompression { get; set; } = true;

}
