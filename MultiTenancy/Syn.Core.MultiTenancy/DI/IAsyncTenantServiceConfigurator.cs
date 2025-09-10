using Microsoft.Extensions.DependencyInjection;

using Syn.Core.MultiTenancy.Metadata;

using System.Threading;
using System.Threading.Tasks;

namespace Syn.Core.MultiTenancy.DI
{
    /// <summary>
    /// Asynchronous variant of <see cref="ITenantServiceConfigurator"/> for scenarios where tenant configuration
    /// requires I/O (e.g., fetching secrets or settings from an external store).
    /// </summary>
    public interface IAsyncTenantServiceConfigurator
    {
        /// <summary>
        /// Applies asynchronous configuration for the given tenant to the service collection used to build its container.
        /// </summary>
        /// <param name="services">The modifiable service collection to configure.</param>
        /// <param name="tenant">The tenant metadata.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        Task ConfigureAsync(IServiceCollection services, TenantInfo tenant, CancellationToken cancellationToken = default);
    }
}
