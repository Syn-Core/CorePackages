using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;


namespace Syn.Core.MultiTenancy.EFCore
{
    /// <summary>
    /// Service registration helpers for tenant-specific EF Core migrations.
    /// </summary>
    public static class TenantMigrationsServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the core services required to run tenant-specific EF Core migrations.
        /// Consumers must provide:
        /// - ITenantConnectionStringResolver
        /// - ITenantSchemaResolver
        /// - Optional ISchemaImpactAnalyzer
        /// </summary>
        public static IServiceCollection AddTenantMigrations(
            this IServiceCollection services,
            Action<TenantMigrationOptions>? configure = null)
        {
            if (configure != null)
                services.Configure(configure);
            else
                services.TryAddSingleton<IOptions<TenantMigrationOptions>>(Options.Create(new TenantMigrationOptions()));

            services.TryAddSingleton<ITenantDbContextFactory, TenantDbContextFactory>();
            services.TryAddSingleton<MigrationRunnerMultiTenant>();

            // Consumers must provide:
            // - ITenantConnectionStringResolver
            // - ITenantSchemaResolver
            // - Optional ISchemaImpactAnalyzer

            return services;
        }
    }
}
