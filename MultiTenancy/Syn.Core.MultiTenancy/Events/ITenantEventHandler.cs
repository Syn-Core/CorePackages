using Syn.Core.MultiTenancy.Metadata;

namespace Syn.Core.MultiTenancy.Events
{
    /// <summary>
    /// Defines event handlers for tenant lifecycle events.
    /// Implement this interface to react to tenant creation, activation, deactivation, deletion, or metadata changes.
    /// </summary>
    public interface ITenantEventHandler
    {
        /// <summary>
        /// Called when a new tenant is created.
        /// </summary>
        Task OnTenantCreatedAsync(TenantInfo tenant, CancellationToken cancellationToken = default);

        /// <summary>
        /// Called when a tenant is activated.
        /// </summary>
        Task OnTenantActivatedAsync(TenantInfo tenant, CancellationToken cancellationToken = default);

        /// <summary>
        /// Called when a tenant is deactivated.
        /// </summary>
        Task OnTenantDeactivatedAsync(TenantInfo tenant, CancellationToken cancellationToken = default);

        /// <summary>
        /// Called when a tenant is deleted.
        /// </summary>
        Task OnTenantDeletedAsync(TenantInfo tenant, CancellationToken cancellationToken = default);

        /// <summary>
        /// Called when a tenant's metadata changes (e.g., feature flags, connection strings).
        /// </summary>
        Task OnTenantMetadataChangedAsync(TenantInfo tenant, CancellationToken cancellationToken = default);
    }
}
