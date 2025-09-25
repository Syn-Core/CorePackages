using Microsoft.EntityFrameworkCore;

using Syn.Core.MultiTenancy.Context;
using Syn.Core.MultiTenancy.Extensions;
using Syn.Core.SqlSchemaGenerator;
using Syn.Core.SqlSchemaGenerator.Extensions;

using System.Linq.Expressions;

namespace Syn.Core.MultiTenancy.EFCore;

/// <summary>
/// Provides extension methods for applying entity definitions to EF Core's ModelBuilder
/// with multi-tenancy support.
///<para>
/// Call this method inside your DbContext's OnModelCreating to apply multi-tenant filters and schema mappings.
/// </para> 
/// This method supports three strategies for identifying the tenant property:
/// 1. Attribute-based: A property decorated with [TenantId].
/// 2. Interface-based: An entity implementing ITenantEntity.
/// 3. Context-based: A property name provided by ITenantContext.TenantPropertyName.
/// 
/// Priority: Attribute → Interface → Context property name.
/// </summary>
public static class ModelBuilderMultiTenantExtensions
{
    /// <summary>
    /// Applies entity definitions to the EF Core model with multi-tenancy query filters.
    /// </summary>
    /// <param name="builder">The EF Core model builder.</param>
    /// <param name="entityTypes">The entity types to configure.</param>
    /// <param name="tenantContext">The current tenant context.</param>
    public static void ApplyEntityDefinitionsToModelMultiTenant(
        this ModelBuilder builder,
        IEnumerable<Type> entityTypes,
        ITenantContext tenantContext)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));
        if (entityTypes == null) throw new ArgumentNullException(nameof(entityTypes));
        if (tenantContext == null) throw new ArgumentNullException(nameof(tenantContext));

        // 🏷 Schema per Tenant (if single active tenant with its own schema)
        if (!string.IsNullOrWhiteSpace(tenantContext.ActiveTenant?.TenantId))
            builder.HasDefaultSchema(tenantContext.ActiveTenant?.SchemaName);

        // 🏷 Shared Schema: Add global query filter for TenantId(s)
        var tenantIds = tenantContext.Tenants
            .Select(t => t.TenantId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();

        foreach (var type in entityTypes)
        {
            var tenantProp = TenantPropertyFinder.Find(type, tenantContext.TenantPropertyName);

            if (tenantProp != null && tenantIds.Any())
            {
                var parameter = Expression.Parameter(type, "e");
                var property = Expression.Property(parameter, tenantProp);

                Expression body;

                if (tenantIds.Count == 1)
                {
                    // e => e.TenantId == singleTenantId
                    var constant = Expression.Constant(tenantIds.First());
                    body = Expression.Equal(property, constant);
                }
                else
                {
                    // e => tenantIds.Contains(e.TenantId)
                    var containsMethod = typeof(List<string>).GetMethod("Contains", new[] { typeof(string) });
                    var constantList = Expression.Constant(tenantIds);
                    body = Expression.Call(constantList, containsMethod!, property);
                }

                var lambda = Expression.Lambda(body, parameter);
                builder.Entity(type).HasQueryFilter(lambda);
            }
        }

        // ✅ Call the core method from SqlSchemaGenerator
        builder.ApplyEntityDefinitionsToModel(entityTypes);
    }


    /// <summary>
    /// Runs the migration process for the current tenant using the connection string
    /// from <see cref="ITenantContext.ActiveTenant"/>.
    /// </summary>
    /// <param name="tenantContext">
    /// The current tenant context containing the active tenant's connection string.
    /// </param>
    /// <param name="entityTypes">
    /// The entity types to include in the migration process.
    /// </param>
    /// <param name="execute">Whether to execute the migration immediately. Default is true.</param>
    /// <param name="dryRun">If true, generates the migration script without applying it.</param>
    /// <param name="interactive">If true, runs the migration in interactive mode.</param>
    /// <param name="previewOnly">If true, shows the migration preview without applying changes.</param>
    /// <param name="autoMerge">If true, automatically merges changes without prompting.</param>
    /// <param name="showReport">If true, generates a migration report after execution.</param>
    /// <param name="impactAnalysis">If true, performs an impact analysis before migration.</param>
    /// <param name="rollbackOnFailure">If true, rolls back changes if migration fails.</param>
    /// <param name="autoExecuteRollback">If true, automatically executes rollback without prompting.</param>
    /// <param name="interactiveMode">The interactive mode to use (e.g., "step").</param>
    /// <param name="rollbackPreviewOnly">If true, previews rollback without executing it.</param>
    /// <param name="stopOnUnsafe"></param>
    /// <param name="logToFile">If true, logs migration output to a file.</param>
    /// <exception cref="ArgumentNullException">Thrown if tenantContext or entityTypes is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if no active tenant or connection string is found.</exception>
    public static void RunMigrationsForTenant(
        this ITenantContext tenantContext,
        IEnumerable<Type> entityTypes,
        bool execute = true,
        bool dryRun = false,
        bool interactive = false,
        bool previewOnly = false,
        bool autoMerge = false,
        bool showReport = false,
        bool impactAnalysis = false,
        bool rollbackOnFailure = true,
        bool autoExecuteRollback = false,
        string interactiveMode = "step",
        bool rollbackPreviewOnly = false,
        bool stopOnUnsafe = true,
        bool logToFile = false)
    {
        if (tenantContext == null) throw new ArgumentNullException(nameof(tenantContext));
        if (entityTypes == null) throw new ArgumentNullException(nameof(entityTypes));

        var connectionString = tenantContext.ActiveTenant?.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("No active tenant or connection string found in ITenantContext.");

        var runner = new MigrationRunner(connectionString);
        runner.Initiate(
            entityTypes,
            execute,
            dryRun,
            interactive,
            previewOnly,
            autoMerge,
            showReport,
            impactAnalysis,
            rollbackOnFailure,
            autoExecuteRollback,
            interactiveMode,
            rollbackPreviewOnly,
            stopOnUnsafe,
            logToFile
        );
    }

    /// <summary>
    /// Runs migrations for all tenants in the provided <see cref="ITenantContext"/>.
    /// This will iterate through each tenant, create a <see cref="MigrationRunner"/> 
    /// with its connection string, and execute the migration process.
    /// </summary>
    /// <param name="tenantContext">
    /// The tenant context containing all known tenants.
    /// </param>
    /// <param name="entityTypes">
    /// The entity types to include in the migration process.
    /// </param>
    /// <param name="execute">Whether to execute the migration immediately. Default is true.</param>
    /// <param name="dryRun">If true, generates the migration script without applying it.</param>
    /// <param name="interactive">If true, runs the migration in interactive mode.</param>
    /// <param name="previewOnly">If true, shows the migration preview without applying changes.</param>
    /// <param name="autoMerge">If true, automatically merges changes without prompting.</param>
    /// <param name="showReport">If true, generates a migration report after execution.</param>
    /// <param name="impactAnalysis">If true, performs an impact analysis before migration.</param>
    /// <param name="rollbackOnFailure">If true, rolls back changes if migration fails.</param>
    /// <param name="autoExecuteRollback">If true, automatically executes rollback without prompting.</param>
    /// <param name="interactiveMode">The interactive mode to use (e.g., "step").</param>
    /// <param name="rollbackPreviewOnly">If true, previews rollback without executing it.</param>
    /// <param name="stopOnUnsafe"></param>
    /// <param name="logToFile">If true, logs migration output to a file.</param>
    /// <exception cref="ArgumentNullException">Thrown if tenantContext or entityTypes is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if a tenant has no connection string.</exception>
    public static void RunMigrationsForAllTenants(
        this ITenantContext tenantContext,
        IEnumerable<Type> entityTypes,
        bool execute = true,
        bool dryRun = false,
        bool interactive = false,
        bool previewOnly = false,
        bool autoMerge = false,
        bool showReport = false,
        bool impactAnalysis = false,
        bool rollbackOnFailure = true,
        bool autoExecuteRollback = false,
        string interactiveMode = "step",
        bool rollbackPreviewOnly = false,
        bool stopOnUnsafe = true,
        bool logToFile = false)
    {
        if (tenantContext == null) throw new ArgumentNullException(nameof(tenantContext));
        if (entityTypes == null) throw new ArgumentNullException(nameof(entityTypes));

        foreach (var tenant in tenantContext.Tenants)
        {
            if (string.IsNullOrWhiteSpace(tenant.ConnectionString))
                throw new InvalidOperationException($"Tenant '{tenant.TenantId}' has no connection string.");

            Console.WriteLine($"[MigrationRunner] Running migrations for tenant: {tenant.TenantId}");

            var runner = new MigrationRunner(tenant.ConnectionString);
            runner.Initiate(
                entityTypes,
                execute,
                dryRun,
                interactive,
                previewOnly,
                autoMerge,
                showReport,
                impactAnalysis,
                rollbackOnFailure,
                autoExecuteRollback,
                interactiveMode,
                rollbackPreviewOnly,
                stopOnUnsafe,
                logToFile
            );
        }
    }






}