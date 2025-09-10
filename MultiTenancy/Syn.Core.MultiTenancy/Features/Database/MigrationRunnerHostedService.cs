using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Syn.Core.MultiTenancy.Metadata;
using Syn.Core.SqlSchemaGenerator;

using System.Diagnostics;

namespace Syn.Core.MultiTenancy.Features.Database
{
    /// <summary>
    /// Hosted service that runs MigrationRunner at application startup
    /// to ensure the TenantFeatureFlags table exists for all known tenants (EF-based provider).
    /// </summary>
    public class EfFeatureFlagsMigrationHostedService<TDbContext> : IHostedService
    where TDbContext : DbContext
    {
        private readonly IServiceProvider _sp;
        private readonly IEnumerable<TenantInfo>? _knownTenants;
        private readonly ILogger<EfFeatureFlagsMigrationHostedService<TDbContext>> _logger;

        public EfFeatureFlagsMigrationHostedService(
            IServiceProvider sp,
            IEnumerable<TenantInfo>? knownTenants,
            ILogger<EfFeatureFlagsMigrationHostedService<TDbContext>> logger)
        {
            _sp = sp;
            _knownTenants = knownTenants;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("=== Starting EF Feature Flags Migration Hosted Service ===");

            // 1) Run migration for Default DbContext
            using (var scope = _sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
                var defaultConn = db.Database.GetConnectionString();

                if (string.IsNullOrWhiteSpace(defaultConn))
                    throw new InvalidOperationException("Default DbContext connection string is null or empty.");

                try
                {
                    _logger.LogInformation("Running migration for DEFAULT database: {Connection}", defaultConn);
                    var sw = Stopwatch.StartNew();

                    var runner = new MigrationRunner(defaultConn);
                    runner.Initiate(new[] { typeof(TenantFeatureFlag) });

                    sw.Stop();
                    _logger.LogInformation("Migration completed for DEFAULT database in {Elapsed} ms.", sw.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Migration failed for DEFAULT database.");
                    throw;
                }
            }

            // 2) Run migration for tenants only if provided
            if (_knownTenants != null && _knownTenants.Any())
            {
                foreach (var tenant in _knownTenants)
                {
                    try
                    {
                        _logger.LogInformation("Running migration for tenant '{TenantId}' with DB: {Connection}",
                            tenant.TenantId, tenant.ConnectionString);

                        var sw = Stopwatch.StartNew();

                        var runner = new MigrationRunner(tenant.ConnectionString);
                        runner.Initiate(new[] { typeof(TenantFeatureFlag) });

                        sw.Stop();
                        _logger.LogInformation("Migration completed for tenant '{TenantId}' in {Elapsed} ms.", tenant.TenantId, sw.ElapsedMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Migration failed for tenant '{TenantId}'.", tenant.TenantId);
                        throw;
                    }
                }
            }
            else
            {
                _logger.LogInformation("No tenants provided — skipping tenant migrations.");
            }

            _logger.LogInformation("=== EF Feature Flags Migration Hosted Service completed ===");

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }



    /// <summary>
    /// Hosted service that runs MigrationRunner at application startup
    /// to ensure the TenantFeatureFlags table exists for all known tenants (SQL-based provider).
    /// </summary>
    public class SqlFeatureFlagsMigrationHostedService : IHostedService
    {
        private readonly string _defaultConnectionString;
        private readonly IEnumerable<TenantInfo> _knownTenants;
        private readonly ILogger<SqlFeatureFlagsMigrationHostedService> _logger;

        public SqlFeatureFlagsMigrationHostedService(
            string defaultConnectionString,
            IEnumerable<TenantInfo> knownTenants,
            ILogger<SqlFeatureFlagsMigrationHostedService> logger)
        {
            _defaultConnectionString = defaultConnectionString;
            _knownTenants = knownTenants;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("=== Starting SQL Feature Flags Migration Hosted Service ===");

            // 1) Default DB
            try
            {
                _logger.LogInformation("Running migration for DEFAULT database: {Connection}", _defaultConnectionString);
                var sw = Stopwatch.StartNew();

                var runner = new MigrationRunner(_defaultConnectionString);
                runner.Initiate(new[] { typeof(TenantFeatureFlag) });

                sw.Stop();
                _logger.LogInformation("Migration completed for DEFAULT database in {Elapsed} ms.", sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Migration failed for DEFAULT database.");
                throw;
            }

            // 2) Tenants (لو فيه)
            if (_knownTenants != null && _knownTenants.Any())
            {
                foreach (var tenant in _knownTenants)
                {
                    try
                    {
                        _logger.LogInformation("Running migration for tenant '{TenantId}' with DB: {Connection}",
                            tenant.TenantId, tenant.ConnectionString);

                        var sw = Stopwatch.StartNew();

                        var runner = new MigrationRunner(tenant.ConnectionString);
                        runner.Initiate(new[] { typeof(TenantFeatureFlag) });

                        sw.Stop();
                        _logger.LogInformation("Migration completed for tenant '{TenantId}' in {Elapsed} ms.",
                            tenant.TenantId, sw.ElapsedMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Migration failed for tenant '{TenantId}'.", tenant.TenantId);
                        throw;
                    }
                }
            }
            else
            {
                _logger.LogInformation("No tenants provided — skipping tenant migrations.");
            }

            _logger.LogInformation("=== SQL Feature Flags Migration Hosted Service completed ===");

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }


}
