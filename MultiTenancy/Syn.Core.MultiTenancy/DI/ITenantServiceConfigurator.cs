using Microsoft.Extensions.DependencyInjection;

using Syn.Core.MultiTenancy.Metadata;

namespace Syn.Core.MultiTenancy.DI
{
    /// <summary>
    /// Configures tenant-specific service registrations during container creation.
    /// </summary>
    public interface ITenantServiceConfigurator
    {
        /// <summary>
        /// Applies configuration for the given tenant to the service collection used to build its container.
        /// </summary>
        /// <param name="services">The modifiable service collection to configure.</param>
        /// <param name="tenant">The tenant metadata.</param>
        void Configure(IServiceCollection services, TenantInfo tenant);
    }
}
