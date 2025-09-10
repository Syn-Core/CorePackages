using Syn.Core.MultiTenancy.Metadata;

namespace Syn.Core.MultiTenancy.DI
{
    /// <summary>
    /// Manages starting and stopping hosted services for all tenants.
    /// </summary>
    public sealed class TenantHostedServiceManager
    {
        private readonly ITenantStore _tenantStore;
        private readonly IEnumerable<ITenantHostedService> _services;

        public TenantHostedServiceManager(ITenantStore tenantStore, IEnumerable<ITenantHostedService> services)
        {
            _tenantStore = tenantStore;
            _services = services;
        }

        public async Task StartAllAsync(CancellationToken cancellationToken)
        {
            var all = await _tenantStore.GetAllAsync();
            foreach (var tenant in all)
            {
                foreach (var service in _services)
                    await service.StartAsync(tenant.TenantId, cancellationToken);
            }
        }

        public async Task StopAllAsync(CancellationToken cancellationToken)
        {
            var all = await _tenantStore.GetAllAsync();
            foreach (var tenant in all)
            {
                foreach (var service in _services)
                    await service.StopAsync(tenant.TenantId, cancellationToken);
            }
        }
    }
}
