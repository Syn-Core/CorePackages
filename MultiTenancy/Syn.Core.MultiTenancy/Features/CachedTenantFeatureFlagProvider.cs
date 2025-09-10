using Microsoft.Extensions.Caching.Memory;

namespace Syn.Core.MultiTenancy.Features
{
    /// <summary>
    /// Caches feature flag checks per tenant to reduce calls to the underlying provider,
    /// with the ability to invalidate cache entries on demand.
    /// </summary>
    public sealed class CachedTenantFeatureFlagProvider : ITenantFeatureFlagProvider
    {
        private readonly ITenantFeatureFlagProvider _inner;
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _cacheDuration;

        public CachedTenantFeatureFlagProvider(
            ITenantFeatureFlagProvider inner,
            IMemoryCache cache,
            TimeSpan cacheDuration)
        {
            _inner = inner;
            _cache = cache;
            _cacheDuration = cacheDuration;
        }

        private string GetCacheKey(string tenantId) => $"tenant-flags:{tenantId}";

        public async Task<IEnumerable<string>> GetEnabledFeaturesAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            var cacheKey = GetCacheKey(tenantId);

            if (_cache.TryGetValue(cacheKey, out IEnumerable<string> cached))
                return cached;

            var flags = await _inner.GetEnabledFeaturesAsync(tenantId, cancellationToken).ConfigureAwait(false);

            _cache.Set(cacheKey, flags, _cacheDuration);

            return flags;
        }

        public async Task<bool> IsEnabledAsync(string tenantId, string featureName, CancellationToken cancellationToken = default)
        {
            var flags = await GetEnabledFeaturesAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return flags.Contains(featureName, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Invalidates the cache for a specific tenant.
        /// </summary>
        public void InvalidateTenant(string tenantId)
        {
            _cache.Remove(GetCacheKey(tenantId));
        }

        /// <summary>
        /// Invalidates the cache for all tenants.
        /// </summary>
        public void InvalidateAll()
        {
            // IMemoryCache ما بيدعمش مسح كل العناصر مباشرة
            // ممكن نستخدم MemoryCache جديد أو نحقن IMemoryCache كـ MemoryCache ونمسحه
            if (_cache is MemoryCache memCache)
            {
                memCache.Compact(1.0); // يمسح كل العناصر
            }
        }
    }
}
