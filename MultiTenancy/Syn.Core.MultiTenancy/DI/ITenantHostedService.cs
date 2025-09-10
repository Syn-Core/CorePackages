
namespace Syn.Core.MultiTenancy.DI
{
    /// <summary>
    /// Represents a hosted service that runs for a specific tenant.
    /// </summary>
    public interface ITenantHostedService
    {
        Task StartAsync(string tenantId, CancellationToken cancellationToken);
        Task StopAsync(string tenantId, CancellationToken cancellationToken);
    }
}
