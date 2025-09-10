namespace Syn.Core.MultiTenancy.DI
{
    /// <summary>
    /// Provides a tenant-specific service provider for resolving services per tenant.
    /// Implementations should cache providers and allow invalidation when tenant config changes.
    /// </summary>
    public interface ITenantScopedServiceProviderFactory
    {
        /// <summary>
        /// Gets (or builds and caches) the service provider associated with the specified tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <returns>A configured <see cref="IServiceProvider"/> for the tenant.</returns>
        IServiceProvider GetProviderForTenant(string tenantId);

        /// <summary>
        /// Invalidates the cached provider for a specific tenant. The next call to
        /// <see cref="GetProviderForTenant"/> will rebuild the provider.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        void Invalidate(string tenantId);

        /// <summary>
        /// Invalidates all cached tenant providers.
        /// Note: IMemoryCache does not support enumeration. If you need true InvalidateAll(), either:
        ///use a versioned prefix in CacheKeyFactory and bump the version, or
        ///wrap IMemoryCache with your own keyed index.See “Future extensions” below for a ready option.
        /// </summary>
        void InvalidateAll();
    }
}
