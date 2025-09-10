using Syn.Core.MultiTenancy.Metadata;

namespace Syn.Core.MultiTenancy.EFCore
{
    /// <summary>
    /// Controls multi-tenant migration execution behavior.
    /// </summary>
    public sealed class TenantMigrationOptions
    {
        /// <summary>
        /// Degree of parallelism for running tenant migrations. Default is 1 (sequential).
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; } = 1;

        /// <summary>
        /// Whether to ignore inactive tenants by default.
        /// </summary>
        public bool IncludeInactiveTenants { get; set; } = false;

        /// <summary>
        /// Optional per-tenant filter: return true to include the tenant in execution.
        /// </summary>
        public Func<TenantInfo, bool>? TenantFilter { get; set; }

        /// <summary>
        /// When true, swallows individual tenant failures and continues. Otherwise, stops on first failure.
        /// </summary>
        public bool ContinueOnError { get; set; } = true;

        /// <summary>
        /// Optional callback invoked before running a tenant's workload.
        /// </summary>
        public Action<string>? OnTenantStart { get; set; }

        /// <summary>
        /// Optional callback invoked after running a tenant's workload with the outcome.
        /// </summary>
        public Action<string, MigrationRunReport>? OnTenantCompleted { get; set; }
    }
}
