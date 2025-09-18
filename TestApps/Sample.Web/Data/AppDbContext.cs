using Microsoft.EntityFrameworkCore;
using Syn.Core.MultiTenancy.Context;
using Syn.Core.Logger;
using Syn.Core.MultiTenancy.EFCore;
using System;
using System.Linq;
using Sample.Web.Entities;

namespace Sample.Web.Data
{
    public class AppDbContext : DbContext
    {
        private readonly ITenantContext _tenantContext;
        private readonly bool _runBulkMigrations;

        public AppDbContext(
            DbContextOptions<AppDbContext> options,
            ITenantContext tenantContext,
            IConfiguration configuration)
            : base(options)
        {
            _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
            _runBulkMigrations = configuration.GetValue<bool>("Migrations:RunForAllTenants");

            ConsoleLog.Info("=== DbContext Initialized ===", customPrefix: "DbContext");

            if (_tenantContext.Tenants != null && _tenantContext.Tenants.Any())
            {
                foreach (var tenant in _tenantContext.Tenants)
                {
                    ConsoleLog.Info($"TenantId: {tenant.TenantId}", customPrefix: "DbContext");
                    ConsoleLog.Info($"SchemaName: {tenant.SchemaName ?? "(default)"}", customPrefix: "DbContext");
                    ConsoleLog.Info($"ConnectionString: {tenant.ConnectionString}", customPrefix: "DbContext");
                }
            }
            else
            {
                ConsoleLog.Warning("No tenants found!", customPrefix: "DbContext");
            }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var entityTypes = typeof(AppDbContext).Assembly
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(EntityBase).IsAssignableFrom(t));

            if (_runBulkMigrations)
            {
                ConsoleLog.Info("Starting BULK migration for all tenants...", customPrefix: "DbContext");
                _tenantContext.RunMigrationsForAllTenants(entityTypes, execute: true, showReport: true);
                ConsoleLog.Info("BULK migration completed.", customPrefix: "DbContext");
            }
            else
            {
                if (_tenantContext.ActiveTenant != null &&
                    !string.IsNullOrWhiteSpace(_tenantContext.ActiveTenant.ConnectionString))
                {
                    optionsBuilder.UseSqlServer(_tenantContext.ActiveTenant.ConnectionString);
                    ConsoleLog.Info($"Starting migration for tenant: {_tenantContext.ActiveTenant.TenantId}", customPrefix: "DbContext");
                    _tenantContext.RunMigrationsForTenant(entityTypes, execute: true, showReport: true);
                    ConsoleLog.Info($"Migration completed for tenant: {_tenantContext.ActiveTenant.TenantId}", customPrefix: "DbContext");
                }
                else
                {
                    ConsoleLog.Warning("No active tenant set for migration!", customPrefix: "DbContext");
                }
            }

            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (_tenantContext.ActiveTenant != null &&
                !string.IsNullOrWhiteSpace(_tenantContext.ActiveTenant.SchemaName))
            {
                modelBuilder.HasDefaultSchema(_tenantContext.ActiveTenant.SchemaName);
                ConsoleLog.Info($"Applied default schema: {_tenantContext.ActiveTenant.SchemaName}", customPrefix: "DbContext");
            }

            base.OnModelCreating(modelBuilder);
        }
    }
}