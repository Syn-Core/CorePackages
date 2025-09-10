using Microsoft.EntityFrameworkCore;

namespace Syn.Core.MultiTenancy.EFCore
{
    /// <summary>
    /// Default EF Core migration runner that optionally performs impact analysis.
    /// </summary>
    public sealed class EfCoreMigrationRunner : IMigrationRunner
    {
        private readonly ITenantDbContextFactory _contextFactory;
        private readonly ISchemaImpactAnalyzer? _impactAnalyzer;
        private readonly string _tenantId;

        /// <summary>
        /// Creates a new runner for a specific tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="contextFactory">Tenant-aware DbContext factory.</param>
        /// <param name="impactAnalyzer">Optional impact analyzer.</param>
        public EfCoreMigrationRunner(
            string tenantId,
            ITenantDbContextFactory contextFactory,
            ISchemaImpactAnalyzer? impactAnalyzer = null)
        {
            _tenantId = tenantId;
            _contextFactory = contextFactory;
            _impactAnalyzer = impactAnalyzer;
        }

        /// <inheritdoc />
        public async Task<MigrationRunReport> InitiateAsync(
            IEnumerable<Type> entityTypes,
            bool executeMigrations,
            bool performImpactAnalysis,
            CancellationToken cancellationToken = default)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string? summary = null;

            try
            {
                await using var context = _contextFactory.Create(_tenantId, entityTypes);

                if (performImpactAnalysis && _impactAnalyzer != null)
                {
                    summary = await _impactAnalyzer.AnalyzeAsync(context, cancellationToken).ConfigureAwait(false);
                }

                if (executeMigrations)
                {
                    await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                }

                return new MigrationRunReport
                {
                    ExecutedMigrations = executeMigrations,
                    PerformedImpactAnalysis = performImpactAnalysis && _impactAnalyzer != null,
                    ImpactSummary = summary,
                    Duration = sw.Elapsed
                };
            }
            catch (Exception ex)
            {
                return new MigrationRunReport
                {
                    ExecutedMigrations = false,
                    PerformedImpactAnalysis = performImpactAnalysis && _impactAnalyzer != null,
                    ImpactSummary = summary,
                    Error = ex,
                    Duration = sw.Elapsed
                };
            }
        }
    }
}
