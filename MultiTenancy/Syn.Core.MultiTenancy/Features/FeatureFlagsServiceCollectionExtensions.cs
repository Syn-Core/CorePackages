using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Syn.Core.MultiTenancy.Configurators;
using Syn.Core.MultiTenancy.Context;
using Syn.Core.MultiTenancy.DI;
using Syn.Core.MultiTenancy.Events;
using Syn.Core.MultiTenancy.Events.Handlers;
using Syn.Core.MultiTenancy.Features.Database;
using Syn.Core.MultiTenancy.Metadata;

namespace Syn.Core.MultiTenancy.Features
{
    public static class TenantFeatureFlagsServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a tenant-specific feature flag provider using the given factory.
        /// This ensures each tenant gets its own ITenantFeatureFlagProvider instance.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="providerFactory">
        /// A factory that creates an ITenantFeatureFlagProvider for a given TenantInfo.
        /// </param>
        public static IServiceCollection AddTenantFeatureFlags(
            this IServiceCollection services,
            Func<TenantInfo, ITenantFeatureFlagProvider> providerFactory)
        {
            if (providerFactory == null) throw new ArgumentNullException(nameof(providerFactory));

            // نستخدم الـ Overload الجديد اللي بياخد instance
            services.AddAsyncTenantConfigurator(
                new FeatureFlagsTenantConfigurator(providerFactory)
            );

            return services;
        }

        /// <summary>
        /// Registers tenant feature flags with caching and automatic cache invalidation
        /// via tenant event hooks, using a custom provider factory.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="providerFactory">
        /// A factory that creates an ITenantFeatureFlagProvider for a given TenantInfo.
        /// </param>
        /// <param name="cacheDuration">The duration to cache feature flags per tenant.</param>
        /// <param name="knownTenants">
        /// Optional list of known tenants to register in the TenantStore if not already registered.
        /// </param>
        public static IServiceCollection AddTenantFeatureFlagsWithEvents(
            this IServiceCollection services,
            Func<TenantInfo, ITenantFeatureFlagProvider> providerFactory,
            TimeSpan cacheDuration,
            IEnumerable<TenantInfo>? knownTenants = null)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (providerFactory == null) throw new ArgumentNullException(nameof(providerFactory));

            // ✅ لو فيه knownTenants ومفيش TenantStore متسجل، نسجله هنا
            if (knownTenants != null && knownTenants.Any())
            {
                services.TryAddSingleton<ITenantStore>(sp =>
                {
                    var cache = sp.GetRequiredService<IMemoryCache>();
                    var store = new InMemoryTenantStore(knownTenants);
                    return new CachedTenantStore(store, cache);
                });
            }

            // Memory cache for caching feature flags
            services.AddMemoryCache();

            // Register Tenant Events infrastructure
            services.TryAddSingleton<TenantEventPublisher>();

            // Register the cache invalidation handler
            services.TryAddSingleton<ITenantEventHandler, FeatureFlagCacheInvalidationHandler>();

            // Register the cached provider per tenant
            services.AddAsyncTenantConfigurator(sp =>
                new FeatureFlagsTenantConfigurator(tenant =>
                {
                    var baseProvider = providerFactory(tenant);

                    if (baseProvider == null)
                        throw new InvalidOperationException(
                            $"The providerFactory returned null for tenant '{tenant?.TenantId ?? "unknown"}'.");

                    var cache = sp.GetRequiredService<IMemoryCache>();
                    return new CachedTenantFeatureFlagProvider(baseProvider, cache, cacheDuration);
                })
            );

            return services;
        }


        /// <summary>
        /// Registers tenant feature flags using EF Core as the storage provider.
        /// </summary>
        /// <typeparam name="TDbContext">The DbContext type containing TenantFeatureFlags DbSet.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <param name="cacheDuration">The duration to cache feature flags per tenant.</param>
        /// <param name="optionsAction">DbContext configuration (e.g., UseSqlServer).</param>
        /// <param name="knownTenants"></param>
        public static IServiceCollection AddTenantFeatureFlags<TDbContext>(
            this IServiceCollection services,
            TimeSpan cacheDuration,
            Action<DbContextOptionsBuilder> optionsAction,
            IEnumerable<TenantInfo>? knownTenants)
            where TDbContext : DbContext
        {
            return services.AddTenantFeatureFlags(
                TenantFeatureFlagProviderType.EfCore,
                cacheDuration,
                knownTenants,
                optionsAction,
                typeof(TDbContext)
            );
        }




        /// <summary>
        /// Registers EF Core-based multi-tenant feature flag services with optional tenants.
        /// </summary>
        /// <remarks>
        /// This overload is intended for scenarios where feature flags are stored in an EF Core DbContext.
        /// It supports both:
        /// <list type="bullet">
        /// <item><description><b>Default-only</b>: No tenants provided, only the default DbContext is used.</description></item>
        /// <item><description><b>Multi-tenant</b>: A list of tenants is provided, and migrations are run for each tenant's database.</description></item>
        /// </list>
        /// The method internally calls the main <c>AddTenantFeatureFlags</c> method with <see cref="TenantFeatureFlagProviderType.EfCore"/>.
        /// </remarks>
        /// <param name="services">The DI service collection.</param>
        /// <param name="cacheDuration">The duration to cache feature flags in memory.</param>
        /// <param name="optionsAction">
        /// Action to configure the EF Core DbContext options (e.g., <c>options.UseSqlServer(...)</c>).
        /// This is required for EF Core provider.
        /// </param>
        /// <param name="dbContextType">
        /// The type of the DbContext to use for feature flags.
        /// Must inherit from <see cref="DbContext"/>.
        /// </param>
        /// <param name="knownTenants">
        /// Optional list of tenants.  
        /// Pass <c>null</c> or an empty list for default-only scenarios.
        /// </param>
        /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="services"/>, <paramref name="optionsAction"/>, or <paramref name="dbContextType"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="dbContextType"/> does not inherit from <see cref="DbContext"/>.
        /// </exception>
        public static IServiceCollection AddTenantFeatureFlags(
            this IServiceCollection services,
            TimeSpan cacheDuration,
            Action<DbContextOptionsBuilder> optionsAction,
            Type dbContextType,
            IEnumerable<TenantInfo>? knownTenants = null)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (optionsAction == null) throw new ArgumentNullException(nameof(optionsAction));
            if (dbContextType == null || !typeof(DbContext).IsAssignableFrom(dbContextType))
                throw new ArgumentException("A valid DbContext type must be provided for EF Core provider.", nameof(dbContextType));

            knownTenants ??= [];

            return services.AddTenantFeatureFlags(
                providerType: TenantFeatureFlagProviderType.EfCore,
                cacheDuration: cacheDuration,
                knownTenants: knownTenants,
                optionsAction: optionsAction,
                dbContextType: dbContextType
            );
        }

        /// <summary>
        /// Registers SQL-based multi-tenant feature flag services with a required default connection string and optional tenants.
        /// </summary>
        /// <remarks>
        /// This overload is intended for scenarios where feature flags are stored in a SQL database without EF Core.
        /// It supports both:
        /// <list type="bullet">
        /// <item><description><b>Default-only</b>: No tenants provided, only the default database is used.</description></item>
        /// <item><description><b>Multi-tenant</b>: A list of tenants is provided, and migrations are run for each tenant's database in addition to the default.</description></item>
        /// </list>
        /// The method internally calls the main <c>AddTenantFeatureFlags</c> method with <see cref="TenantFeatureFlagProviderType.Sql"/>.
        /// </remarks>
        /// <param name="services">The DI service collection.</param>
        /// <param name="cacheDuration">The duration to cache feature flags in memory.</param>
        /// <param name="defaultConnectionString">
        /// The connection string for the default database.  
        /// This is always required, even if no tenants are provided.
        /// </param>
        /// <param name="knownTenants">
        /// Optional list of tenants.  
        /// Pass <c>null</c> or an empty list for default-only scenarios.
        /// </param>
        /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="services"/> or <paramref name="defaultConnectionString"/> is null or empty.
        /// </exception>
        public static IServiceCollection AddTenantFeatureFlags(
            this IServiceCollection services,
            TimeSpan cacheDuration,
            string defaultConnectionString,
            IEnumerable<TenantInfo>? knownTenants = null)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrWhiteSpace(defaultConnectionString))
                throw new ArgumentNullException(nameof(defaultConnectionString), "Default connection string is required.");

            knownTenants ??= [];

            return services.AddTenantFeatureFlags(
                providerType: TenantFeatureFlagProviderType.Sql,
                cacheDuration: cacheDuration,
                knownTenants: knownTenants,
                defaultConnectionString: defaultConnectionString
            );
        }






        /// <summary>
        /// Registers multi-tenant feature flag services for either EF Core or SQL provider,
        /// including caching, migration hosted services, and event handling.
        /// </summary>
        /// <param name="services">The DI service collection.</param>
        /// <param name="providerType">The provider type to use (EF Core or SQL).</param>
        /// <param name="cacheDuration">The duration to cache feature flags in memory.</param>
        /// <param name="knownTenants">
        /// A list of known tenants.  
        /// Can be empty if you only want to work with the default database.
        /// </param>
        /// <param name="optionsAction">
        /// EF Core only: Action to configure the DbContext options (e.g., UseSqlServer).
        /// Required if <paramref name="providerType"/> is EF Core.
        /// </param>
        /// <param name="dbContextType">
        /// EF Core only: The type of the DbContext to use for feature flags.
        /// Required if <paramref name="providerType"/> is EF Core.
        /// </param>
        /// <param name="defaultConnectionString">
        /// SQL only: The connection string for the default database.
        /// Required if <paramref name="providerType"/> is SQL.
        /// </param>
        /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
        public static IServiceCollection AddTenantFeatureFlags(
            this IServiceCollection services,
            TenantFeatureFlagProviderType providerType,
            TimeSpan cacheDuration,
            IEnumerable<TenantInfo>? knownTenants,
            Action<DbContextOptionsBuilder>? optionsAction = null,
            Type? dbContextType = null,
            string? defaultConnectionString = null)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            knownTenants ??= [];

            // ✅ لو فيه knownTenants ومفيش TenantStore متسجل، نسجله هنا
            services.TryAddSingleton<ITenantStore>(sp =>
            {
                var cache = sp.GetRequiredService<IMemoryCache>();
                var store = new InMemoryTenantStore(knownTenants);
                return new CachedTenantStore(store, cache);
            });

            services.AddMemoryCache();

            switch (providerType)
            {
                case TenantFeatureFlagProviderType.EfCore:
                    if (optionsAction == null)
                        throw new ArgumentNullException(nameof(optionsAction), "EF Core provider requires DbContext options configuration.");
                    if (dbContextType == null || !typeof(DbContext).IsAssignableFrom(dbContextType))
                        throw new ArgumentException("A valid DbContext type must be provided for EF Core provider.", nameof(dbContextType));

                    // Register DbContext dynamically WITH model customizer
                    var addDbContextMethod = typeof(EntityFrameworkServiceCollectionExtensions)
                        .GetMethods()
                        .First(m => m.Name == "AddDbContext" && m.GetGenericArguments().Length == 1)
                        .MakeGenericMethod(dbContextType);

                    Action<DbContextOptionsBuilder> wrappedOptionsAction = options =>
                    {
                        optionsAction(options);
                        options.ReplaceService<IModelCustomizer, TenantFeatureFlagModelCustomizer>();
                    };

                    addDbContextMethod.Invoke(
                        null,
                        new object?[] { services, wrappedOptionsAction, ServiceLifetime.Scoped, ServiceLifetime.Scoped }
                    );

                    // Register HostedService dynamically
                    var hostedServiceType = typeof(EfFeatureFlagsMigrationHostedService<>).MakeGenericType(dbContextType);
                    services.AddSingleton(knownTenants); // Can be empty for EF default-only scenario
                    services.AddSingleton(typeof(IHostedService), hostedServiceType);

                    // Register CachedTenantFeatureFlagProvider in DI
                    services.AddSingleton<CachedTenantFeatureFlagProvider>(sp =>
                    {
                        var db = (DbContext)sp.GetRequiredService(dbContextType);
                        var providerTypeInstance = typeof(EfDatabaseTenantFeatureFlagProvider<>).MakeGenericType(dbContextType);
                        var provider = (ITenantFeatureFlagProvider)Activator.CreateInstance(providerTypeInstance, db)!;
                        return new CachedTenantFeatureFlagProvider(provider, sp.GetRequiredService<IMemoryCache>(), cacheDuration);
                    });

                    break;

                case TenantFeatureFlagProviderType.Sql:
                    if (string.IsNullOrWhiteSpace(defaultConnectionString))
                        throw new ArgumentNullException(nameof(defaultConnectionString), "SQL provider requires a default connection string.");

                    // Register default SQL provider
                    services.AddSingleton(new SqlDatabaseTenantFeatureFlagProvider(defaultConnectionString));
                    services.AddSingleton(new CachedTenantFeatureFlagProvider(
                        new SqlDatabaseTenantFeatureFlagProvider(defaultConnectionString),
                        services.BuildServiceProvider().GetRequiredService<IMemoryCache>(),
                        cacheDuration
                    ));

                    // Register HostedService for SQL migrations
                    services.AddSingleton(knownTenants);
                    services.AddHostedService(sp =>
                        new SqlFeatureFlagsMigrationHostedService(
                            defaultConnectionString,
                            knownTenants,
                            sp.GetRequiredService<ILogger<SqlFeatureFlagsMigrationHostedService>>()
                        )
                    );

                    break;

                default:
                    throw new NotSupportedException($"Provider type '{providerType}' is not supported.");
            }

            // Register Event system after provider is available
            services.AddSingleton<ITenantEventHandler, FeatureFlagCacheInvalidationHandler>();
            services.AddSingleton<TenantEventPublisher>();

            // Register AsyncTenantConfigurator using the provider from DI
            services.AddAsyncTenantConfigurator(sp =>
                new FeatureFlagsTenantConfigurator(_ =>
                {
                    return sp.GetRequiredService<CachedTenantFeatureFlagProvider>();
                })
            );

            return services;
        }


        /// <summary>
        /// Registers common services for both EF and SQL providers.
        /// </summary>
        private static void RegisterCommonServices(IServiceCollection services)
        {
            services.AddMemoryCache();
            services.AddSingleton<ITenantEventHandler, FeatureFlagCacheInvalidationHandler>();
            services.AddSingleton<TenantEventPublisher>();
        }



    }
}
