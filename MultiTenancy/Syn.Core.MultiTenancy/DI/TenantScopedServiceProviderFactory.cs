using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Syn.Core.MultiTenancy.Metadata;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Syn.Core.MultiTenancy.DI
{
    /// <summary>
    /// Builds and caches tenant-specific service providers from a base service collection,
    /// applying both synchronous and asynchronous configurators per tenant.
    /// </summary>
    public sealed class TenantScopedServiceProviderFactory : ITenantScopedServiceProviderFactory, IDisposable
    {
        private readonly IServiceCollectionSnapshot _baseSnapshot;
        private readonly IMemoryCache _cache;
        private readonly TenantContainerOptions _options;
        private readonly IServiceProvider _rootProvider;

        private readonly IEnumerable<ITenantServiceConfigurator> _syncConfigurators;
        private readonly IEnumerable<IAsyncTenantServiceConfigurator> _asyncConfigurators;

        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="TenantScopedServiceProviderFactory"/> class.
        /// </summary>
        /// <param name="baseSnapshot">Snapshot of the base service collection to clone per tenant.</param>
        /// <param name="cache">In-memory cache used to store tenant providers.</param>
        /// <param name="options">Options controlling caching and lifecycle.</param>
        /// <param name="rootProvider">
        /// The root application service provider to resolve configurators and shared services.
        /// </param>
        public TenantScopedServiceProviderFactory(
            IServiceCollectionSnapshot baseSnapshot,
            IMemoryCache cache,
            IOptions<TenantContainerOptions> options,
            IServiceProvider rootProvider)
        {
            _baseSnapshot = baseSnapshot ?? throw new ArgumentNullException(nameof(baseSnapshot));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _rootProvider = rootProvider ?? throw new ArgumentNullException(nameof(rootProvider));

            _syncConfigurators = _rootProvider.GetServices<ITenantServiceConfigurator>();
            _asyncConfigurators = _rootProvider.GetServices<IAsyncTenantServiceConfigurator>();
        }

        /// <inheritdoc />
        public IServiceProvider GetProviderForTenant(string tenantId)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID must be provided.", nameof(tenantId));

            ThrowIfDisposed();

            var cacheKey = _options.CacheKeyFactory(tenantId);

            return _cache.GetOrCreate(cacheKey, entry =>
            {
                ApplyCacheEntryOptions(entry);

                // Build synchronously; async configurators are honored via a sync-over-async barrier.
                return BuildProviderForTenant(tenantId, CancellationToken.None).GetAwaiter().GetResult();
            })!;
        }

        /// <inheritdoc />
        public void Invalidate(string tenantId)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                return;

            ThrowIfDisposed();

            var cacheKey = _options.CacheKeyFactory(tenantId);
            if (_options.DisposeOnInvalidate && _cache.TryGetValue(cacheKey, out var existing) && existing is IDisposable d)
                d.Dispose();

            _cache.Remove(cacheKey);
        }

        /// <inheritdoc />
        public void InvalidateAll()
        {
            // IMemoryCache does not provide enumeration; rely on caller to track keys or use a versioned key prefix.
            // Practical approach: bump a version token in CacheKeyFactory if needed. For now, this is a no-op hint.
            // Consider adding a keyed cache wrapper if global invalidation is required at runtime.
        }

        /// <summary>
        /// Disposes the factory and prevents further provider creation.
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
        }

        private async Task<IServiceProvider> BuildProviderForTenant(string tenantId, CancellationToken cancellationToken)
        {
            var services = _baseSnapshot.Clone();

            // Make tenant id available during building, if needed by configurators.
            services.AddSingleton(new TenantContainerBuildContext(tenantId));

            // Run sync configurators
            foreach (var cfg in _syncConfigurators)
                cfg.Configure(services, ResolveTenantInfo(tenantId));

            // Run async configurators
            foreach (var cfg in _asyncConfigurators)
                await cfg.ConfigureAsync(services, ResolveTenantInfo(tenantId), cancellationToken).ConfigureAwait(false);

            var provider = services.BuildServiceProvider();

            _options.OnProviderBuilt?.Invoke(tenantId, provider);
            return provider;
        }

        private static void ApplyCacheEntryOptions(ICacheEntry entry)
        {
            // Note: The options are captured in the closure via _options; set on the entry here.
            var factory = (TenantScopedServiceProviderFactory)entry.PostEvictionCallbacks[0].State!;
            var opts = factory._options;

            entry.SlidingExpiration = opts.SlidingExpiration;
            if (opts.AbsoluteExpirationRelativeToNow.HasValue)
                entry.AbsoluteExpirationRelativeToNow = opts.AbsoluteExpirationRelativeToNow;
        }

        private TenantInfo ResolveTenantInfo(string tenantId)
        {
            var store = _rootProvider.GetRequiredService<ITenantStore>();
            var tenant = store.GetAsync(tenantId).GetAwaiter().GetResult();
            if (tenant == null)
                throw new InvalidOperationException($"Tenant '{tenantId}' not found.");
            return tenant;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TenantScopedServiceProviderFactory));
        }
    }
}
