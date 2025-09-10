using Microsoft.Extensions.Caching.Memory;

using System.Collections.Concurrent;

namespace Syn.Core.MultiTenancy.DI
{
    /// <summary>
    /// A cache wrapper that tracks keys to allow global invalidation of tenant service providers.
    /// </summary>
    public sealed class IndexedTenantProviderCache
    {
        private readonly IMemoryCache _cache;
        private readonly ConcurrentDictionary<string, byte> _keys = new();

        public IndexedTenantProviderCache(IMemoryCache cache)
        {
            _cache = cache;
        }

        public object? GetOrCreate(string key, Func<ICacheEntry, object> factory)
        {
            _keys.TryAdd(key, 0);
            return _cache.GetOrCreate(key, factory);
        }

        public void Remove(string key)
        {
            _cache.Remove(key);
            _keys.TryRemove(key, out _);
        }

        public void RemoveAll()
        {
            foreach (var key in _keys.Keys)
            {
                _cache.Remove(key);
                _keys.TryRemove(key, out _);
            }
        }
    }
}
