using Microsoft.EntityFrameworkCore;
using Syn.Core.MultiTenancy.Context;
using Syn.Core.MultiTenancy.Metadata;
using Syn.Core.Logger;
using Sample.Web.Entities;

namespace Sample.Web.Data
{
    public class AppDbContext : DbContext
    {
        private readonly ITenantContext _tenantContext;

        public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
            : base(options)
        {
            _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));

            // 🔹 طباعة معلومات التينانت عند إنشاء الـ DbContext
            if (_tenantContext.ActiveTenant != null)
            {
                ConsoleLog.Info($"[DbContext Init] TenantId: {_tenantContext.ActiveTenant.TenantId}", customPrefix: "DbContext");
                ConsoleLog.Info($"[DbContext Init] SchemaName: {_tenantContext.ActiveTenant.SchemaName ?? "(default)"}", customPrefix: "DbContext");
                ConsoleLog.Info($"[DbContext Init] ConnectionString: {_tenantContext.ActiveTenant.ConnectionString}", customPrefix: "DbContext");
            }
            else
            {
                ConsoleLog.Warning("[DbContext Init] No active tenant set!", customPrefix: "DbContext");
            }
        }

        public DbSet<Order> Orders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // لو فيه Tenant نشط وله SchemaName، نطبقه
            if (_tenantContext.ActiveTenant != null &&
                !string.IsNullOrWhiteSpace(_tenantContext.ActiveTenant.SchemaName))
            {
                modelBuilder.HasDefaultSchema(_tenantContext.ActiveTenant.SchemaName);
            }

            base.OnModelCreating(modelBuilder);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // لو كل Tenant ليه Connection String مختلف
            if (_tenantContext.ActiveTenant != null &&
                !string.IsNullOrWhiteSpace(_tenantContext.ActiveTenant.ConnectionString))
            {
                optionsBuilder.UseSqlServer(_tenantContext.ActiveTenant.ConnectionString);
            }

            base.OnConfiguring(optionsBuilder);
        }
    }
}