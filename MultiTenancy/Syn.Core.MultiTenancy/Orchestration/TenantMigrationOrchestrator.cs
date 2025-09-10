using Syn.Core.MultiTenancy.Metadata;
using Syn.Core.SqlSchemaGenerator;

namespace Syn.Core.MultiTenancy.Orchestration
{
    /// <summary>
    /// Orchestrates schema migrations for one or more tenants using the core MigrationRunner.
    /// </summary>
    public class TenantMigrationOrchestrator
    {
        private readonly ITenantStore _tenantStore;

        /// <summary>
        /// Creates a new instance of the orchestrator.
        /// </summary>
        /// <param name="tenantStore">The tenant store providing tenant metadata.</param>
        public TenantMigrationOrchestrator(ITenantStore tenantStore)
        {
            _tenantStore = tenantStore ?? throw new ArgumentNullException(nameof(tenantStore));
        }

        /// <summary>
        /// Runs migrations for all tenants in the store.
        /// </summary>
        /// <param name="entityTypes">The entity types to include in the model.</param>
        /// <param name="execute">Whether to execute the migration scripts against the database.</param>
        /// <param name="impactAnalysis">Whether to generate an impact analysis report.</param>
        public async Task MigrateAllTenantsAsync(
            IEnumerable<Type> entityTypes,
            bool execute = true,
            bool impactAnalysis = true)
        {
            var tenants = await _tenantStore.GetAllAsync();

            foreach (var tenant in tenants)
            {
                await MigrateTenantAsync(tenant, entityTypes, execute, impactAnalysis);
            }
        }

        /// <summary>
        /// Runs migrations for a specific tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="entityTypes">The entity types to include in the model.</param>
        /// <param name="execute">Whether to execute the migration scripts against the database.</param>
        /// <param name="impactAnalysis">Whether to generate an impact analysis report.</param>
        public async Task MigrateTenantAsync(
            string tenantId,
            IEnumerable<Type> entityTypes,
            bool execute = true,
            bool impactAnalysis = true)
        {
            var tenant = await _tenantStore.GetAsync(tenantId)
                ?? throw new KeyNotFoundException($"Tenant '{tenantId}' not found.");

            await MigrateTenantAsync(tenant, entityTypes, execute, impactAnalysis);
        }

        /// <summary>
        /// Internal method to run migrations for a given tenant.
        /// </summary>
        private Task MigrateTenantAsync(
            TenantInfo tenant,
            IEnumerable<Type> entityTypes,
            bool execute,
            bool impactAnalysis)
        {
            Console.WriteLine($"[Multi-Tenant Migration] Starting migration for tenant '{tenant.TenantId}'...");

            var runner = new MigrationRunner(tenant.ConnectionString);
            runner.Initiate(
                entityTypes,
                execute: execute,
                previewOnly: false,
                impactAnalysis: impactAnalysis
            );

            Console.WriteLine($"[Multi-Tenant Migration] Completed migration for tenant '{tenant.TenantId}'.");
            return Task.CompletedTask;
        }
    }
}