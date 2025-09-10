using Syn.Core.MultiTenancy.Metadata;

namespace Syn.Core.MultiTenancy.DI
{
    public sealed class TenantHealthCheckRunner
    {
        private readonly ITenantStore _tenantStore;
        private readonly IEnumerable<ITenantHealthCheck> _checks;

        public TenantHealthCheckRunner(ITenantStore tenantStore, IEnumerable<ITenantHealthCheck> checks)
        {
            _tenantStore = tenantStore;
            _checks = checks;
        }

        public async Task<IDictionary<string, IEnumerable<HealthCheckResult>>> RunAllAsync(CancellationToken cancellationToken = default)
        {
            var results = new Dictionary<string, IEnumerable<HealthCheckResult>>();
            var all = await _tenantStore.GetAllAsync();
            foreach (var tenant in all)
            {
                var tenantResults = new List<HealthCheckResult>();
                foreach (var check in _checks)
                {
                    tenantResults.Add(await check.CheckAsync(tenant.TenantId, cancellationToken));
                }
                results[tenant.TenantId] = tenantResults;
            }

            return results;
        }
    }
}
