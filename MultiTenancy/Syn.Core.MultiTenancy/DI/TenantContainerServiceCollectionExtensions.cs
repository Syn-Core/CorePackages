using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Syn.Core.MultiTenancy.DI
{
    /// <summary>
    /// Service registration helpers for tenant-specific DI containers.
    /// </summary>
    public static class TenantContainerServiceCollectionExtensions
    {
        /// <summary>
        /// Adds tenant container infrastructure to the DI container.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="baseServices">
        /// The base services to be cloned for each tenant.
        /// If null, the current 'services' collection is snapshotted at call time.
        /// </param>
        /// <param name="configure">Optional options configuration.</param>
        public static IServiceCollection AddTenantContainers(
            this IServiceCollection services,
            IServiceCollection? baseServices = null,
            Action<TenantContainerOptions>? configure = null)
        {
            services.TryAddSingleton<IMemoryCache, MemoryCache>();

            if (configure != null)
                services.Configure(configure);
            else
                services.TryAddSingleton<IOptions<TenantContainerOptions>>(Options.Create(new TenantContainerOptions()));

            // Snapshot the base services
            services.AddSingleton<IServiceCollectionSnapshot>(sp =>
            {
                var source = baseServices ?? services;
                return new ServiceCollectionSnapshot(source);
            });

            // Factory
            services.TryAddSingleton<ITenantScopedServiceProviderFactory, TenantScopedServiceProviderFactory>();

            return services;
        }

        /// <summary>
        /// Registers a synchronous tenant configurator.
        /// </summary>
        public static IServiceCollection AddTenantConfigurator<T>(this IServiceCollection services)
            where T : class, ITenantServiceConfigurator
        {
            services.AddSingleton<ITenantServiceConfigurator, T>();
            return services;
        }

        /// <summary>
        /// Registers an asynchronous tenant configurator.
        /// </summary>
        public static IServiceCollection AddAsyncTenantConfigurator<T>(this IServiceCollection services)
            where T : class, IAsyncTenantServiceConfigurator
        {
            services.AddSingleton<IAsyncTenantServiceConfigurator, T>();
            return services;
        }

        public static IServiceCollection AddAsyncTenantConfigurator(
            this IServiceCollection services,
            IAsyncTenantServiceConfigurator instance)
        {
            services.AddSingleton(typeof(IAsyncTenantServiceConfigurator), instance);
            return services;
        }

        /// <summary>
        /// Registers an asynchronous tenant configurator.
        /// </summary>
        public static IServiceCollection AddAsyncTenantConfigurator(
            this IServiceCollection services,
            Func<IServiceProvider, IAsyncTenantServiceConfigurator> factory)
        {
            services.AddSingleton(typeof(IAsyncTenantServiceConfigurator), sp => factory(sp));
            return services;
        }
    }
}
