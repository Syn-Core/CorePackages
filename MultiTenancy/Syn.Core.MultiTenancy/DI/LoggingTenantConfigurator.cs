using Microsoft.Extensions.DependencyInjection;

using Syn.Core.MultiTenancy.Metadata;

namespace Syn.Core.MultiTenancy.DI
{
    public sealed class LoggingTenantConfigurator : ITenantServiceConfigurator
    {
        public void Configure(IServiceCollection services, TenantInfo tenant)
        {
            services.AddSingleton<ITenantLogger>(new TenantLogger(tenant.TenantId));
        }
    }
}
