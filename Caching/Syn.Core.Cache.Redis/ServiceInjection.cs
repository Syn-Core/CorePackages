using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Syn.Core.Cache.Redis.Caching;
using Syn.Core.Cache.Redis.Compression;
using Syn.Core.Cache.Redis.Configuration;
using Syn.Core.Cache.Redis.Encryption;
using Syn.Core.Cache.Redis.Interfaces;
using Syn.Core.Cache.Redis.Serializers;
using Syn.Core.Cache.Redis.Sessions;
using Syn.Core.Cache.Redis.Transformers;

public static class ServiceInjection
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="services"></param>
    /// <param name="EncryptionKey"></param>
    /// <param name="EncryptionIV"></param>
    /// <param name="configure"></param>
    /// <param name="redisMoreOptions"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    public static IServiceCollection AddRedisCache(this IServiceCollection services, string EncryptionKey, string EncryptionIV, Action<RedisCacheOptions> configure, Action<RCOptions> redisMoreOptions = null)
    {
        if(configure == null)
            throw new ArgumentNullException(nameof(configure), "RedisCacheOptions configuration cannot be null.");

        // Register Redis cache with provided options
        services.AddStackExchangeRedisCache(configure);

        services.AddSingleton<IDataCompressor, BrotliCompressor>();

        services.AddSingleton<IDataEncryptor>(provider =>
        {
            if (string.IsNullOrWhiteSpace(EncryptionKey) || string.IsNullOrWhiteSpace(EncryptionIV))
            {
                throw new InvalidOperationException("Encryption key and IV must be configured in appsetting.json as Encryption:Key, Encryption:IV.");
            }
            return new AesEncryptor(EncryptionKey, EncryptionIV);
        });

        // typeof(ISerializer<>), typeof(MessagePackSerializer<>)
        services.AddSingleton(typeof(ISerializer), provider =>
        {
            var options = provider.GetRequiredService<IOptions<RCOptions>>().Value;
            return options.Serializer switch
            {
                "MessagePack" => typeof(MessagePackSerializer),
                "Json" => typeof(JsonSerialize),
                _ => throw new NotSupportedException($"Serialization format {options.Serializer} is not supported, please use (MessagePack) or (Json).")
            };
        });
        services.AddSingleton<IDataTransformer, DataTransformer>();
        services.AddSingleton(typeof(ISecureCache), typeof(SecureDistributedCache));

        services.AddSingleton<ConfigureRCOptionsFactory>();
        services.AddScoped<ISessionManager, SessionManager>();


        return services;
    }
}
