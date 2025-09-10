using Microsoft.Extensions.Caching.Memory;

using Syn.Core.MultiTenancy.Features.Internal;

namespace Syn.Core.MultiTenancy.Features
{
    /// <summary>
    /// Decorator that caches feature flags per tenant to reduce source lookups.
    /// </summary>
    public sealed class CachedFeatureFlagSource : IFeatureFlagSource
    {
        private readonly IFeatureFlagSource _inner;
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _cacheDuration;

        public CachedFeatureFlagSource(IFeatureFlagSource inner, IMemoryCache cache, TimeSpan cacheDuration)
        {
            _inner = inner;
            _cache = cache;
            _cacheDuration = cacheDuration;
        }

        public async Task<IEnumerable<string>>? GetFlagsAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"tenant-flags:{tenantId}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<string>? cachedFlags))
                return cachedFlags;

            var flags = await _inner.GetFlagsAsync(tenantId, cancellationToken).ConfigureAwait(false);

            _cache.Set(cacheKey, flags, _cacheDuration);

            return flags;
        }
    }
}
