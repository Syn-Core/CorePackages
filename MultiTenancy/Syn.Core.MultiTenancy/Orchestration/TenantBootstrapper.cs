using Microsoft.EntityFrameworkCore;

using Syn.Core.MultiTenancy.Metadata;
using Syn.Core.SqlSchemaGenerator;

namespace Syn.Core.MultiTenancy.Orchestration
{
    /// <summary>
    /// Bootstraps a new tenant by creating its database/schema, running migrations,
    /// and optionally seeding initial data.
    /// </summary>
    public class TenantBootstrapper
    {
        private readonly ITenantStore _tenantStore;
        private readonly TenantMigrationOrchestrator _migrationOrchestrator;

        /// <summary>
        /// Creates a new instance of the bootstrapper.
        /// </summary>
        /// <param name="tenantStore">The tenant store to persist tenant metadata.</param>
        /// <param name="migrationOrchestrator">The orchestrator to run migrations.</param>
        public TenantBootstrapper(
            ITenantStore tenantStore,
            TenantMigrationOrchestrator migrationOrchestrator)
        {
            _tenantStore = tenantStore ?? throw new ArgumentNullException(nameof(tenantStore));
            _migrationOrchestrator = migrationOrchestrator ?? throw new ArgumentNullException(nameof(migrationOrchestrator));
        }

        /// <summary>
        /// Bootstraps a new tenant.
        /// </summary>
        /// <param name="tenantInfo">The tenant metadata.</param>
        /// <param name="entityTypes">The entity types to include in the model.</param>
        /// <param name="seedAction">Optional action to seed initial data for the tenant.</param>
        public async Task BootstrapTenantAsync(
            TenantInfo tenantInfo,
            IEnumerable<Type> entityTypes,
            Func<DbContext, Task>? seedAction = null)
        {
            // 1️⃣ Add tenant to store
            await _tenantStore.AddOrUpdateAsync(tenantInfo);

            // 2️⃣ Ensure database/schema exists and run migrations
            await _migrationOrchestrator.MigrateTenantAsync(tenantInfo.TenantId, entityTypes);

            // 3️⃣ Seed initial data if provided
            if (seedAction != null)
            {
                var options = new DbContextOptionsBuilder<DbContext>()
                    .UseSqlServer(tenantInfo.ConnectionString)
                    .Options;

                using var db = new DbContext(options);
                await seedAction(db);
            }

            Console.WriteLine($"[Tenant Bootstrapper] Tenant '{tenantInfo.TenantId}' bootstrapped successfully.");
        }
    }
}