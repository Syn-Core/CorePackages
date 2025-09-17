using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using Syn.Core.MultiTenancy.Context;
using Syn.Core.MultiTenancy.EFCore;
using Syn.Core.MultiTenancy.Features;
using Syn.Core.MultiTenancy.Features.Database;
using Syn.Core.MultiTenancy.Metadata;
using Syn.Core.MultiTenancy.Resolution;

namespace Syn.Core.MultiTenancy;

/// <summary>
/// Provides extension methods for registering multi-tenancy services in the DI container.
/// </summary>
public static partial class MultiTenancyServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core multi-tenancy services, including:
    /// <list type="bullet">
    /// <item>Configuration for <see cref="MultiTenancyOptions"/>.</item>
    /// <item>Tenant context accessor (<see cref="ITenantContextAccessor"/>).</item>
    /// <item>Memory cache for tenant-related caching.</item>
    /// <item>Composite tenant resolution strategy combining claims, headers, query strings, domain, and subdomain.</item>
    /// </list>
    /// </summary>
    /// <param name="services">
    /// The <see cref="IServiceCollection"/> to add the services to.
    /// </param>
    /// <param name="configure">
    /// Optional configuration action for <see cref="MultiTenancyOptions"/>.
    /// </param>
    /// <param name="strategyFactory">
    /// Optional factory to create a custom <see cref="ITenantResolutionStrategy"/>.
    /// If not provided, a default composite strategy will be used with keys from <see cref="MultiTenancyOptions"/>.
    /// </param>
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
            services.TryAddSingleton<ITenantResolutionStrategy>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<MultiTenancyOptions>>().Value;

                return new CompositeTenantResolutionStrategy(new ITenantResolutionStrategy[]
                {
                // Claims
                new ClaimTenantResolutionStrategy(opts.ClaimKey ?? "tenant_id"),

                // Header
                new HeaderTenantResolutionStrategy(opts.HeaderKey ?? "X-Tenant-ID"),

                // QueryString
                new QueryStringTenantResolutionStrategy(opts.QueryKey ?? "tenantId"),

                // Domain
                new DomainTenantResolutionStrategy(
                    opts.DomainRegexPattern ?? @"^(?<tenant>[^.]+)\.example\.com$"
                ),

                // Subdomain (improved version)
                new SubdomainTenantResolutionStrategy(
                    rootDomains: opts.RootDomains ?? ["example.com"],
                    includeAllSubLevels: opts.IncludeAllSubLevels,
                    excludedSubdomains: opts.ExcludedSubdomains
                )
                });
            });
        }

        return services;
    }


    /// <summary>
    /// Registers full multi-tenancy services including:
    /// <list type="bullet">
    /// <item>Core services from <see cref="AddMultitenancyCore"/> (with dynamic resolution keys from <see cref="MultiTenancyOptions"/>).</item>
    /// <item>Default tenant store (InMemory + Cached) if not overridden.</item>
    /// <item>Tenant cache exposure via <see cref="ITenantCache"/>.</item>
    /// <item>EF Core SaveChanges interceptor if enabled in options.</item>
    /// <item>Scoped <see cref="ITenantContext"/> built from the tenant store and resolution strategy.</item>
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
        services.AddHttpContextAccessor();

        // Register core services (with dynamic keys from options)
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

            // Resolve tenantId(s) from current context
            var tenantIds = strategy.ResolveTenantIds(
                sp.GetService<IHttpContextAccessor>()?.HttpContext ?? new object());

            // Load all tenants from store
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
    /// Registers full multi-tenancy services together with tenant-specific feature flags
    /// by internally invoking <see cref="AddMultiTenancy"/> and <see cref="TenantFeatureFlagsServiceCollectionExtensions.AddTenantFeatureFlags(IServiceCollection, TenantFeatureFlagProviderType, TimeSpan, IEnumerable{TenantInfo}?, Action{DbContextOptionsBuilder}?, Type?, string?)"/>.
    /// </summary>
    /// <remarks>
    /// This method acts as a convenience wrapper to configure both multi-tenancy and feature flag
    /// infrastructure in a single call, ensuring consistent setup and avoiding code duplication.
    /// Any changes made to <see cref="AddMultiTenancy"/> or <see cref="TenantFeatureFlagsServiceCollectionExtensions.AddTenantFeatureFlags(IServiceCollection, TenantFeatureFlagProviderType, TimeSpan, IEnumerable{TenantInfo}?, Action{DbContextOptionsBuilder}?, Type?, string?)"/>
    /// will automatically be reflected here.
    /// 
    /// <para>
    /// Example usage:
    /// <code>
    /// services.AddMultiTenancyWithFeatureFlags(
    ///     configure: opts =>
    ///     {
    ///         opts.ClaimKey = "tid";
    ///         opts.HeaderKey = "X-Tid";
    ///         opts.QueryKey = "tid";
    ///         opts.DomainRegexPattern = @"^(?&lt;tenant&gt;[^.]+)\.example\.com$";
    ///         opts.RootDomains = new[] { "example.com" };
    ///         opts.IncludeAllSubLevels = false;
    ///         opts.ExcludedSubdomains = new[] { "www", "app" };
    ///     },
    ///     providerType: TenantFeatureFlagProviderType.EfCore,
    ///     cacheDuration: TimeSpan.FromMinutes(10),
    ///     knownTenants: myTenants,
    ///     optionsAction: dbOpts => dbOpts.UseSqlServer("..."),
    ///     dbContextType: typeof(MyTenantDbContext)
    /// );
    /// </code>
    /// </para>
    /// </remarks>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configure">
    /// Delegate to configure <see cref="MultiTenancyOptions"/>.
    /// </param>
    /// <param name="providerType">The provider type to use for feature flags (EF Core or SQL).</param>
    /// <param name="cacheDuration">The duration to cache feature flags in memory.</param>
    /// <param name="knownTenants">
    /// Optional list of known tenants. Can be empty if you only want to work with the default database.
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
    /// <param name="strategyFactory">
    /// Optional factory to create a custom <see cref="ITenantResolutionStrategy"/>.
    /// </param>
    /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddMultiTenancyWithFeatureFlags(
        this IServiceCollection services,
        Action<MultiTenancyOptions> configure,
        TenantFeatureFlagProviderType providerType,
        TimeSpan cacheDuration,
        IEnumerable<TenantInfo>? knownTenants = null,
        Action<DbContextOptionsBuilder>? optionsAction = null,
        Type? dbContextType = null,
        string? defaultConnectionString = null,
        Func<IServiceProvider, ITenantResolutionStrategy>? strategyFactory = null)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        // 1️⃣ Register Multi-Tenancy
        services.AddMultiTenancy(configure, strategyFactory);

        // 2️⃣ Register Tenant Feature Flags (now without overriding ITenantContext)
        services.AddTenantFeatureFlags(
            providerType,
            cacheDuration,
            knownTenants,
            optionsAction,
            dbContextType,
            defaultConnectionString
        );

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
