using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Syn.Core.MultiTenancy.DI
{
    public interface ITenantHealthCheck
    {
        Task<HealthCheckResult> CheckAsync(string tenantId, CancellationToken cancellationToken = default);
    }
}
