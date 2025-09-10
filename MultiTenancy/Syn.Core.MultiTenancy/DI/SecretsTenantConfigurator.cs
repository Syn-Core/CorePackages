using Microsoft.Extensions.DependencyInjection;

using Syn.Core.MultiTenancy.Metadata;

namespace Syn.Core.MultiTenancy.DI
{
    /// <summary>
    /// Configurator that injects secrets into the tenant's service collection.
    /// </summary>
    public sealed class SecretsTenantConfigurator : IAsyncTenantServiceConfigurator
    {
        private readonly ITenantSecretProvider _secretProvider;

        public SecretsTenantConfigurator(ITenantSecretProvider secretProvider)
        {
            _secretProvider = secretProvider;
        }

        public async Task ConfigureAsync(IServiceCollection services, TenantInfo tenant, CancellationToken cancellationToken = default)
        {
            var apiKey = await _secretProvider.GetSecretAsync(tenant.TenantId, "ApiKey", cancellationToken);
            if (!string.IsNullOrEmpty(apiKey))
            {
                services.AddSingleton(new TenantApiKey(apiKey));
            }
        }
    }
}
