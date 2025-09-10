using Microsoft.Extensions.DependencyInjection;

using Syn.Core.MultiTenancy.DI;
using Syn.Core.MultiTenancy.Features;
using Syn.Core.MultiTenancy.Metadata;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Syn.Core.MultiTenancy.Configurators
{
    /// <summary>
    /// Configures ITenantFeatureFlagProvider for each tenant using a factory that can vary per tenant.
    /// </summary>
    public sealed class FeatureFlagsTenantConfigurator : IAsyncTenantServiceConfigurator
    {
        private readonly Func<TenantInfo, ITenantFeatureFlagProvider> _providerFactory;

        /// <summary>
        /// Initializes a new instance with a factory that returns a feature flag provider per tenant.
        /// </summary>
        public FeatureFlagsTenantConfigurator(Func<TenantInfo, ITenantFeatureFlagProvider> providerFactory)
        {
            _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        }

        public Task ConfigureAsync(IServiceCollection services, TenantInfo tenant, CancellationToken cancellationToken = default)
        {
            var provider = _providerFactory(tenant);
            services.AddSingleton(provider);
            return Task.CompletedTask;
        }
    }
}
