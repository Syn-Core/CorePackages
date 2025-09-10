using Syn.Core.MultiTenancy.EFCore;

public sealed class MigrateAllTenantsCommand
{
    private readonly MigrationRunnerMultiTenant _runner;

    public MigrateAllTenantsCommand(MigrationRunnerMultiTenant runner)
    {
        _runner = runner;
    }

    public async Task<int> ExecuteAsync(IEnumerable<Type> entities, bool execute, bool analyze, string[]? tenantIds = null, CancellationToken ct = default)
    {
        var summary = await _runner.RunAsync(
            entities,
            executeMigrations: execute,
            performImpactAnalysis: analyze,
            tenantIds: tenantIds,
            cancellationToken: ct);

        Console.WriteLine($"Total: {summary.TotalTenants}, Succeeded: {summary.Succeeded}, Failed: {summary.Failed}");
        foreach (var kv in summary.Reports)
        {
            var id = kv.Key;
            var r = kv.Value;
            var status = r.Error is null ? "OK" : $"FAILED: {r.Error.Message}";
            Console.WriteLine($"- {id}: {status} | Duration: {r.Duration}");
            if (!string.IsNullOrWhiteSpace(r.ImpactSummary))
                Console.WriteLine($"  Impact: {r.ImpactSummary}");
        }

        return summary.Failed == 0 ? 0 : 1;
    }
}
