using Syn.Core.MultiTenancy.Features;
using Syn.Core.MultiTenancy.Metadata;

namespace Syn.Core.MultiTenancy.Events.Handlers
{
    /// <summary>
    /// Invalidates cached feature flags when tenant metadata changes or tenant is deleted.
    /// </summary>
    public sealed class FeatureFlagCacheInvalidationHandler : ITenantEventHandler
    {
        private readonly CachedTenantFeatureFlagProvider _cachedProvider;

        public FeatureFlagCacheInvalidationHandler(CachedTenantFeatureFlagProvider cachedProvider)
        {
            _cachedProvider = cachedProvider;
        }

        public Task OnTenantCreatedAsync(TenantInfo tenant, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task OnTenantActivatedAsync(TenantInfo tenant, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task OnTenantDeactivatedAsync(TenantInfo tenant, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task OnTenantDeletedAsync(TenantInfo tenant, CancellationToken cancellationToken = default)
        {
            _cachedProvider.InvalidateTenant(tenant.TenantId);
            return Task.CompletedTask;
        }

        public Task OnTenantMetadataChangedAsync(TenantInfo tenant, CancellationToken cancellationToken = default)
        {
            _cachedProvider.InvalidateTenant(tenant.TenantId);
            return Task.CompletedTask;
        }
    }
}
