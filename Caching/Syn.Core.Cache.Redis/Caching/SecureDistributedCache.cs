using Microsoft.Extensions.Caching.Distributed;

using Syn.Core.Cache.Redis.Interfaces;

namespace Syn.Core.Cache.Redis.Caching;

public class SecureDistributedCache : ISecureCache
{
    private readonly IDistributedCache _cache;
    private readonly IDataTransformer _transformer;

    public SecureDistributedCache(
        IDistributedCache cache,
        IDataTransformer transformer)
    {
        _cache = cache;
        _transformer = transformer;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        var transformed = _transformer.Encode(value);

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(30)
        };

        await _cache.SetAsync(key, transformed, options);
    }

    public async Task<T> GetAsync<T>(string key)
    {
        var transformed = await _cache.GetAsync(key);
        if (transformed is null) return default;

        return _transformer.Decode<T>(transformed);
    }

    public async Task RemoveAsync(string key)
    {
        await _cache.RemoveAsync(key);
    }
}


