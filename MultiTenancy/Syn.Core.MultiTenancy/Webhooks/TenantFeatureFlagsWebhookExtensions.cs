using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

using Syn.Core.MultiTenancy.Configurators;
using Syn.Core.MultiTenancy.DI;
using Syn.Core.MultiTenancy.Events;
using Syn.Core.MultiTenancy.Events.Handlers;
using Syn.Core.MultiTenancy.Features;
using Syn.Core.MultiTenancy.Metadata;
using Syn.Core.MultiTenancy.Webhooks;

namespace Syn.Core.MultiTenancy
{
    public static class TenantFeatureFlagsWebhookExtensions
    {
        /// <summary>
        /// Registers webhook handler services for LaunchDarkly and Azure App Configuration.
        /// </summary>
        public static IServiceCollection AddTenantFeatureFlagsWithEvents(
    this IServiceCollection services,
    Func<TenantInfo, ITenantFeatureFlagProvider> providerFactory,
    TimeSpan cacheDuration)
        {
            services.AddMemoryCache();
            services.AddSingleton<TenantEventPublisher>();
            services.AddSingleton<ITenantEventHandler, FeatureFlagCacheInvalidationHandler>();

            services.AddSingleton<FeatureFlagsWebhookHandler>();

            services.AddAsyncTenantConfigurator(
                new FeatureFlagsTenantConfigurator(tenant =>
                {
                    var baseProvider = providerFactory(tenant);
                    var cache = services.BuildServiceProvider().GetRequiredService<IMemoryCache>();
                    return new CachedTenantFeatureFlagProvider(baseProvider, cache, cacheDuration);
                })
            );

            return services;
        }
    }
}