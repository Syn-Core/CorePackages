using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Syn.Core.MultiTenancy.Context;
using Syn.Core.MultiTenancy.EFCore;
using Syn.Core.MultiTenancy.Metadata;
using Syn.Core.MultiTenancy.Resolution;

namespace Syn.Core.MultiTenancy;

/// <summary>
/// Provides extension methods for registering multi-tenancy services in the DI container.
/// </summary>
public static partial class MultiTenancyServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core multi-tenancy services, including tenant resolution strategy,
    /// tenant context accessor, tenant store, and tenant context implementation.
    /// </summary>
    /// <param name="services">
    /// The <see cref="IServiceCollection"/> to add the services to.
    /// </param>
    /// <param name="configure">
    /// Optional configuration action for <see cref="MultiTenancyOptions"/>.
    /// </param>
    /// <param name="strategyFactory">
    /// Optional factory to create a custom <see cref="ITenantResolutionStrategy"/>.
    /// If not provided, a default composite strategy will be used:
    /// Claim → Header → QueryString.
    /// </param>
    /// <typeparam name="TTenantStore">
    /// The type of the tenant store implementation to use for retrieving tenant information.
    /// </typeparam>
    /// <returns>
    /// The same <see cref="IServiceCollection"/> instance so that multiple calls can be chained.
    /// </returns>
    public static IServiceCollection AddMultitenancyCore(
        this IServiceCollection services,
        Action<MultiTenancyOptions>? configure = null,
        Func<IServiceProvider, ITenantResolutionStrategy>? strategyFactory = null)
    {
        // Apply options configuration
        if (configure != null)
            services.Configure(configure);
        else
            services.Configure<MultiTenancyOptions>(_ => { });

        // Ensure IMemoryCache is available
        services.TryAddSingleton<IMemoryCache, MemoryCache>();

        // TenantContext accessor
        services.TryAddSingleton<ITenantContextAccessor, TenantContextAccessor>();

        // Register resolution strategy
        if (strategyFactory != null)
        {
            services.AddSingleton(strategyFactory);
        }
        else
        {
            // Default composite strategy: Claim → Header → QueryString
            services.TryAddSingleton<ITenantResolutionStrategy>(sp =>
                new CompositeTenantResolutionStrategy(new ITenantResolutionStrategy[]
                {
                new ClaimTenantResolutionStrategy("tenant_id"),
                new HeaderTenantResolutionStrategy("X-Tenant-ID"),
                new QueryStringTenantResolutionStrategy("tenantId")
                }));
        }

        return services;
    }


    /// <summary>
    /// Registers full multi-tenancy services including:
    /// <list type="bullet">
    /// <item>Core services from <see cref="AddMultitenancyCore"/>.</item>
    /// <item>Default tenant store (InMemory + Cached) if not overridden.</item>
    /// <item>Tenant cache exposure via <see cref="ITenantCache"/>.</item>
    /// <item>EF Core SaveChanges interceptor if enabled in options.</item>
    /// </list>
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configure">
    /// Optional delegate to configure <see cref="MultiTenancyOptions"/>.
    /// </param>
    /// <param name="strategyFactory">
    /// Optional factory to create a custom <see cref="ITenantResolutionStrategy"/>.
    /// </param>
    /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddMultiTenancy(
        this IServiceCollection services,
        Action<MultiTenancyOptions>? configure = null,
        Func<IServiceProvider, ITenantResolutionStrategy>? strategyFactory = null)
    {
        // Register core services
        services.AddMultitenancyCore(configure, strategyFactory);

        // Build options to use here
        var options = new MultiTenancyOptions();
        configure?.Invoke(options);

        // Register TenantStore (configurable + cached)
        services.AddSingleton<ITenantStore>(sp =>
        {
            var store = options.TenantStoreFactory(sp);
            var cache = sp.GetRequiredService<IMemoryCache>();
            return store is CachedTenantStore ? store : new CachedTenantStore(store, cache);
        });

        // Also expose ITenantCache
        services.TryAddSingleton<ITenantCache>(sp => (ITenantCache)sp.GetRequiredService<ITenantStore>());

        // EF Core SaveChanges interceptor
        if (options.UseTenantInterceptor)
        {
            services.AddScoped<TenantSaveChangesInterceptor>();
        }

        // Register ITenantContext (MultiTenantContext) as Scoped using resolution strategy + tenant store
        services.TryAddScoped<ITenantContext>(sp =>
        {
            var strategy = sp.GetRequiredService<ITenantResolutionStrategy>();
            var tenantStore = sp.GetRequiredService<ITenantStore>();

            // Resolve tenantId from current context
            var tenantIds = strategy.ResolveTenantIds(sp.GetService<IHttpContextAccessor>()?.HttpContext ?? new object());
            var tenants = tenantStore.GetAll().ToList();

            var ctx = new MultiTenantContext(tenants);

            // Set active tenant if resolved and exists
            var resolvedTenantId = tenantIds.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(resolvedTenantId))
                ctx.SetActiveTenant(resolvedTenantId);

            return ctx;
        });

        return services;
    }

    /// <summary>
    /// Adds the tenant resolution middleware to the ASP.NET Core pipeline.
    /// This middleware resolves the current tenant(s) for each request and populates the <see cref="ITenantContext"/>.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The updated <see cref="IApplicationBuilder"/>.</returns>
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TenantResolutionMiddleware>();
    }
}
