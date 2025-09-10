using System;

namespace Syn.Core.MultiTenancy.DI
{
    /// <summary>
    /// Options controlling tenant container creation, caching, and behavior.
    /// </summary>
    public sealed class TenantContainerOptions
    {
        /// <summary>
        /// Sliding expiration for cached tenant service providers. Default is 30 minutes.
        /// </summary>
        public TimeSpan SlidingExpiration { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Optional absolute expiration for cached providers. When set, providers expire after the specified time.
        /// </summary>
        public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }

        /// <summary>
        /// Whether to dispose the previous provider when invalidated or evicted. Default is true.
        /// </summary>
        public bool DisposeOnInvalidate { get; set; } = true;

        /// <summary>
        /// Optional hook to post-process the freshly built provider for a tenant before caching it.
        /// </summary>
        public Action<string, IServiceProvider>? OnProviderBuilt { get; set; }

        /// <summary>
        /// Optional naming strategy for cache keys. If not provided, "tenant-sp:{tenantId}" is used.
        /// </summary>
        public Func<string, string> CacheKeyFactory { get; set; } = tenantId => $"tenant-sp:{tenantId}";
    }
}
