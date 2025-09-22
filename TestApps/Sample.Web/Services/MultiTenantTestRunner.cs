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

            string email = $"{tenant.TenantId}@example.com";
            string fullName = $"Default Customer for {tenant.TenantId}";
            var customer = db.Set<Customer>()
    .FirstOrDefault(c => c.Email == email
                            && c.FullName == fullName);

            if (customer == null)
            {
                customer = new Customer
                {
                    FullName = $"Default Customer for {tenant.TenantId}",
                    Email = $"{tenant.TenantId}@example.com",
                    CreatedAt = DateTime.UtcNow,
                    TenantId = tenant.TenantId
                };
                db.Add(customer);
            }

            db.Add(new Order
            {
                Name = $"Order for {tenant.TenantId}",
                OrderNumber = Guid.NewGuid().ToString("N").Substring(0, 20),
                OrderDate = DateTime.Now,
                TotalAmount = 0m,
                Customer = customer,
                TenantId = tenant.TenantId
            });

            await db.SaveChangesAsync(cancellationToken);

            // 🆕 تخزين بيانات في TenantRuntimeData.Items بدل IMemoryCache العام
            var runtimeData = tenantContext.GetOrAddTenantData(tenant.TenantId, id => new TenantRuntimeData
            {
                Info = tenant,
                ActivatedAt = DateTime.UtcNow
            });

            runtimeData.Items["Key1"] = $"Value for {tenant.TenantId}";
        }

        // 🆕 التحقق من العزل بين التينانتس
        using var verifyScope = _serviceProvider.CreateScope();
        var verifyTenantContext = verifyScope.ServiceProvider.GetRequiredService<ITenantContext>();

        foreach (var tenant in verifyTenantContext.Tenants)
        {
            foreach (var otherTenant in verifyTenantContext.Tenants.Where(t => t != tenant))
            {
                var otherRuntimeData = verifyTenantContext.GetTenantData(otherTenant.TenantId);
                var value = otherRuntimeData?.Items.TryGetValue("Key1", out var v) == true ? v as string : null;

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
