using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;

using Sample.Web.Data;
using Sample.Web.Entities;

using Syn.Core.Logger;
using Syn.Core.MultiTenancy.Context;

using System.Text;

public class MultiTenantTestRunner : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public MultiTenantTestRunner(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        foreach (var tenant in tenantContext.Tenants)
        {
            tenantContext.SetActiveTenant(tenant.TenantId);

            ConsoleLog.Info($"===== Starting tests for Tenant: {tenant.TenantId} =====", customPrefix: "Test");
            ConsoleLog.Info($"ConnectionString: {tenant.ConnectionString}", customPrefix: "Test");
            ConsoleLog.Info($"SchemaName: {tenant.SchemaName ?? "(default)"}", customPrefix: "Test");

            using var tenantScope = _serviceProvider.CreateScope();
            var db = tenantScope.ServiceProvider.GetRequiredService<AppDbContext>();

            ConsoleLog.Info($"Applying migrations for {tenant.TenantId}", customPrefix: "Test");
            await db.Database.MigrateAsync(cancellationToken);

            var script = db.Database.GenerateCreateScript();
            var unsafeCount = RunDropSafetyAudit(script, tenant.TenantId);
            if (unsafeCount > 0)
                ConsoleLog.Warning($"Unsafe DROPs detected for {tenant.TenantId}: {unsafeCount}", customPrefix: "Audit");
            else
                ConsoleLog.Success($"No unsafe DROPs for {tenant.TenantId}", customPrefix: "Audit");

            db.Add(new Order { Name = $"Order for {tenant.TenantId}" });
            await db.SaveChangesAsync(cancellationToken);

            var cache = tenantScope.ServiceProvider.GetRequiredService<IMemoryCache>();
            cache.Set($"{tenant.TenantId}:Key1", $"Value for {tenant.TenantId}");
        }

        using var verifyScope = _serviceProvider.CreateScope();
        var verifyTenantContext = verifyScope.ServiceProvider.GetRequiredService<ITenantContext>();
        var verifyCache = verifyScope.ServiceProvider.GetRequiredService<IMemoryCache>();

        foreach (var tenant in verifyTenantContext.Tenants)
        {
            foreach (var otherTenant in verifyTenantContext.Tenants.Where(t => t != tenant))
            {
                var value = verifyCache.Get<string>($"{otherTenant.TenantId}:Key1");
                if (value != null)
                    ConsoleLog.Error($"Cache isolation failed: {tenant.TenantId} can see {otherTenant.TenantId}'s data", customPrefix: "Cache");
                else
                    ConsoleLog.Success($"Cache isolation OK between {tenant.TenantId} and {otherTenant.TenantId}", customPrefix: "Cache");
            }
        }

        ConsoleLog.Info("===== Multi-tenant tests completed =====", customPrefix: "Test");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private int RunDropSafetyAudit(string finalScript, string tenantId)
    {
        var lines = finalScript.Split('\n');
        var safeDrops = new List<string>();
        var unsafeDrops = new List<string>();

        bool insideBlockComment = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i];
            var line = rawLine.Trim();

            if (line.StartsWith("/*")) insideBlockComment = true;
            if (insideBlockComment)
            {
                if (line.EndsWith("*/")) insideBlockComment = false;
                continue;
            }

            if (line.StartsWith("--")) continue;

            bool isDrop =
                (line.StartsWith("ALTER TABLE", StringComparison.OrdinalIgnoreCase) && line.Contains("DROP", StringComparison.OrdinalIgnoreCase)) ||
                line.StartsWith("DROP INDEX", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("DROP TRIGGER", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("DROP VIEW", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("DROP TABLE", StringComparison.OrdinalIgnoreCase);

            if (isDrop)
            {
                int prev = i - 1;
                while (prev >= 0 && (string.IsNullOrWhiteSpace(lines[prev]) || lines[prev].TrimStart().StartsWith("--"))) prev--;

                if (prev >= 0 && lines[prev].Contains("-- SAFE DROP", StringComparison.OrdinalIgnoreCase))
                    safeDrops.Add($"[Line {i + 1}] {line}");
                else
                    unsafeDrops.Add($"[Line {i + 1}] {line}");
            }
        }

        ConsoleLog.Info($"===== DROP Safety Audit for {tenantId} =====", customPrefix: "Audit");

        if (safeDrops.Any())
        {
            ConsoleLog.Success($"SAFE DROPs ({safeDrops.Count}):", customPrefix: "Audit");
            foreach (var drop in safeDrops)
                ConsoleLog.Info($"  {drop}", customPrefix: "Audit");
        }

        if (unsafeDrops.Any())
        {
            ConsoleLog.Warning($"UNSAFE DROPs ({unsafeDrops.Count}):", customPrefix: "Audit");
            foreach (var drop in unsafeDrops)
                ConsoleLog.Warning($"  {drop}", customPrefix: "Audit");
        }

        var auditReport = new StringBuilder();
        auditReport.AppendLine($"# DROP Safety Audit for {tenantId}");
        auditReport.AppendLine($"## SAFE DROPs ({safeDrops.Count})");
        safeDrops.ForEach(d => auditReport.AppendLine($"- {d}"));
        auditReport.AppendLine($"## UNSAFE DROPs ({unsafeDrops.Count})");
        unsafeDrops.ForEach(d => auditReport.AppendLine($"- {d}"));

        File.WriteAllText($"impact_audit_{tenantId}.md", auditReport.ToString());

        return unsafeDrops.Count;
    }
}