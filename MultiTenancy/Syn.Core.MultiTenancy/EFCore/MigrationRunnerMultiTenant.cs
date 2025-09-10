using Microsoft.Extensions.Options;

using Syn.Core.MultiTenancy.Metadata;
using Syn.Core.MultiTenancy.Resolvers;

namespace Syn.Core.MultiTenancy.EFCore
{
    /// <summary>
    /// Orchestrates running migrations and/or impact analysis across multiple tenants.
    /// </summary>
    public sealed class MigrationRunnerMultiTenant
    {
        private readonly ITenantStore _tenantStore;
        private readonly ITenantDbContextFactory _dbContextFactory;
        private readonly ISchemaImpactAnalyzer? _impactAnalyzer;
        private readonly TenantMigrationOptions _options;

        /// <summary>
        /// Initializes a new instance of <see cref="MigrationRunnerMultiTenant"/>.
        /// </summary>
        public MigrationRunnerMultiTenant(
            ITenantStore tenantStore,
            ITenantDbContextFactory dbContextFactory,
            IOptions<TenantMigrationOptions> options,
            ISchemaImpactAnalyzer? impactAnalyzer = null)
        {
            _tenantStore = tenantStore;
            _dbContextFactory = dbContextFactory;
            _options = options.Value;
            _impactAnalyzer = impactAnalyzer;
        }

        /// <summary>
        /// Executes migrations and/or impact analysis for the specified tenants.
        /// </summary>
        /// <param name="entityTypes">Entity types included in the model.</param>
        /// <param name="executeMigrations">Apply migrations if true.</param>
        /// <param name="performImpactAnalysis">Run impact analysis if true.</param>
        /// <param name="tenantIds">Optional explicit tenant list; when null, uses store.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task<MultiTenantMigrationSummary> RunAsync(
            IEnumerable<Type> entityTypes,
            bool executeMigrations,
            bool performImpactAnalysis,
            IEnumerable<string>? tenantIds = null,
            CancellationToken cancellationToken = default)
        {
            var allTenants = tenantIds is null
                ? await _tenantStore.GetAllAsync()
                : (await _tenantStore.GetAllAsync()).Where(t => tenantIds.Contains(t.TenantId));

            if (!_options.IncludeInactiveTenants)
                allTenants = allTenants.Where(t => t.IsActive);

            if (_options.TenantFilter != null)
                allTenants = allTenants.Where(_options.TenantFilter);

            var tenants = allTenants.ToList();
            var reports = new Dictionary<string, MigrationRunReport>(StringComparer.OrdinalIgnoreCase);

            var sw = System.Diagnostics.Stopwatch.StartNew();

            _ = tenants; // ensure enumeration not repeated
            async Task processTenantAsync(TenantInfo t)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _options.OnTenantStart?.Invoke(t.TenantId);

                var runner = new EfCoreMigrationRunner(
                    t.TenantId,
                    _dbContextFactory,
                    _impactAnalyzer);

                var report = await runner.InitiateAsync(
                    entityTypes,
                    executeMigrations,
                    performImpactAnalysis,
                    cancellationToken).ConfigureAwait(false);

                lock (reports) { reports[t.TenantId] = report; }

                _options.OnTenantCompleted?.Invoke(t.TenantId, report);

                if (report.Error != null && !_options.ContinueOnError)
                    throw new AggregateException($"Tenant {t.TenantId} failed.", report.Error);
            }

            if (_options.MaxDegreeOfParallelism <= 1)
            {
                foreach (var t in tenants)
                    await processTenantAsync(t).ConfigureAwait(false);
            }
            else
            {
                var throttler = new SemaphoreSlim(_options.MaxDegreeOfParallelism);
                var tasks = tenants.Select(async t =>
                {
                    await throttler.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try { await processTenantAsync(t).ConfigureAwait(false); }
                    finally { throttler.Release(); }
                });
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            var summary = new MultiTenantMigrationSummary
            {
                TotalTenants = tenants.Count,
                Succeeded = reports.Count(kv => kv.Value.Error is null),
                Failed = reports.Count(kv => kv.Value.Error is not null),
                Reports = new Dictionary<string, MigrationRunReport>(reports),
                TotalDuration = sw.Elapsed
            };

            return summary;
        }
    }
}
