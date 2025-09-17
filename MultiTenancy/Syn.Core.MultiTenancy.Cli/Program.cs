using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Syn.Core.MultiTenancy;
using Syn.Core.MultiTenancy.Metadata;
using Syn.Core.MultiTenancy.Orchestration;
using Syn.Core.SqlSchemaGenerator;
using Syn.Core.SqlSchemaGenerator.Builders;
using Syn.Core.SqlSchemaGenerator.Report;

using System.CommandLine;
using System.ComponentModel.DataAnnotations.Schema;

partial class Program
{
    static async Task<int> Main(string[] args)
    {
        var startupAssemblyOption = new Option<string?>(
            "--startup-assembly",
            "Path or name of the startup assembly containing Program.cs"
        );

        IServiceProvider? serviceProvider = null;

        // نحاول نجيب الـ ServiceProvider من الـ Startup Project
        var parsed = new RootCommand { startupAssemblyOption };
        parsed.SetHandler((string? startupAsm) =>
        {
            serviceProvider = TryGetStartupServiceProvider(startupAsm);
        }, startupAssemblyOption);
        await parsed.InvokeAsync(args);

        // لو مش لاقيينه، نبني Host داخلي
        if (serviceProvider == null)
        {
            Console.WriteLine("ℹ️ No existing ServiceProvider found. Building internal CLI host...");
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddMultiTenancy(opt =>
                    {
                        opt.DefaultTenantPropertyName = "TenantId";
                        opt.UseTenantInterceptor = false;
                    });

                    services.AddScoped<TenantMigrationOrchestrator>();
                    services.AddScoped<TenantBootstrapper>();
                })
                .Build();

            serviceProvider = host.Services;
        }

        var root = new RootCommand("Multi-Tenancy CLI Tool");

        // ===== Common Options =====
        var tenantOption = new Option<string>(
            "--tenant",
            () => "all",
            "Tenant ID or 'all' for all tenants"
        );

        var assembliesOption = new Option<string?>(
            "--entities-assemblies",
            "Comma-separated list of assemblies (paths or names) containing entity types"
        );

        // ===== migrate =====
        var migrateCmd = new Command("migrate", "Run migrations for tenants")
    {
        tenantOption, assembliesOption
    };
        migrateCmd.SetHandler(async (string tenantId, string? assembliesCsv) =>
        {
            var orchestrator = serviceProvider!.GetRequiredService<TenantMigrationOrchestrator>();
            var assemblies = LoadAssemblies(assembliesCsv);
            var entityTypes = assemblies.SelectMany(a => a.GetTypes()).ToList();

            if (tenantId == "all")
                await orchestrator.MigrateAllTenantsAsync(entityTypes);
            else
                await orchestrator.MigrateTenantAsync(tenantId, entityTypes);
        }, tenantOption, assembliesOption);

        // ===== bootstrap =====
        var idOption = new Option<string>("--id", "Tenant ID") { IsRequired = true };
        var connOption = new Option<string>("--conn", "Connection string") { IsRequired = true };
        var schemaOption = new Option<string?>("--schema", "Schema name");
        var displayOption = new Option<string?>("--display", "Display name");

        var bootstrapCmd = new Command("bootstrap", "Bootstrap a new tenant")
    {
        idOption, connOption, schemaOption, displayOption, assembliesOption
    };
        bootstrapCmd.SetHandler(async (string id, string conn, string? schema, string? display, string? assembliesCsv) =>
        {
            var bootstrapper = serviceProvider!.GetRequiredService<TenantBootstrapper>();
            var assemblies = LoadAssemblies(assembliesCsv);
            var entityTypes = assemblies.SelectMany(a => a.GetTypes()).ToList();

            var tenantInfo = new TenantInfo(id, conn, schema, display);
            await bootstrapper.BootstrapTenantAsync(tenantInfo, entityTypes);
        }, idOption, connOption, schemaOption, displayOption, assembliesOption);

        // ===== list =====
        var listCmd = new Command("list", "List all tenants");
        listCmd.SetHandler(async () =>
        {
            var store = serviceProvider!.GetRequiredService<ITenantStore>();
            var tenants = await store.GetAllAsync();
            foreach (var t in tenants)
                Console.WriteLine($"{t.TenantId} | {t.DisplayName} | {t.SchemaName} | Active: {t.IsActive}");
        });

        // ===== impact =====
        var filterTypesOption = new Option<string?>(
            "--filter-types",
            "Comma-separated list of base classes or interfaces to filter entity types (optional)"
        );

        var htmlReportOption = new Option<string?>(
            "--html-report",
            "Path to save HTML report (optional)"
        );

        var impactCmd = new Command("impact", "Run schema impact analysis without applying changes")
    {
        tenantOption, assembliesOption, filterTypesOption, htmlReportOption
    };

        impactCmd.SetHandler(async (string tenantId, string? assembliesCsv, string? filterTypesCsv, string? htmlReportPath) =>
        {
            var store = serviceProvider!.GetRequiredService<ITenantStore>();
            var tenants = tenantId == "all"
                ? await store.GetAllAsync()
                : new List<TenantInfo> { await store.GetAsync(tenantId) ?? throw new Exception($"Tenant '{tenantId}' not found") };

            var assemblies = LoadAssemblies(assembliesCsv);

            // Resolve filter types
            var filterTypes = new List<Type>();
            if (!string.IsNullOrWhiteSpace(filterTypesCsv))
            {
                foreach (var typeName in filterTypesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    foreach (var asm in assemblies)
                    {
                        var t = asm.GetType(typeName, throwOnError: false);
                        if (t != null)
                        {
                            filterTypes.Add(t);
                            break;
                        }
                    }
                }
            }

            // Gather and filter entity types
            var entityTypes = assemblies
                .SelectMany(a => a.GetTypes())
                .Where(t =>
                    t.IsClass &&
                    !t.IsAbstract &&
                    (
                        (filterTypes.Count > 0 && filterTypes.Any(ft => ft.IsAssignableFrom(t))) ||
                        t.GetCustomAttributes(typeof(TableAttribute), true).Any()
                    )
                )
                .ToList();

            var allImpactItems = new List<(string Tenant, ImpactItem Item)>();

            foreach (var tenant in tenants)
            {
                Console.WriteLine($"\n=== Impact Analysis for Tenant: {tenant.TenantId} ===");

                using var connection = new SqlConnection(tenant.ConnectionString);
                var dbReader = new DatabaseSchemaReader(connection);

                var oldSchema = dbReader.GetAllEntities();
                var builder = new EntityDefinitionBuilder();
                var newSchema = builder.BuildAllWithRelationships(entityTypes);

                foreach (var newEntity in newSchema)
                {
                    var oldEntity = oldSchema.FirstOrDefault(e =>
                        e.Schema.Equals(newEntity.Schema, StringComparison.OrdinalIgnoreCase) &&
                        e.Name.Equals(newEntity.Name, StringComparison.OrdinalIgnoreCase));

                    var impactItems = Analyze.AnalyzeImpact(oldEntity, newEntity);

                    foreach (var item in impactItems)
                    {
                        allImpactItems.Add((tenant.TenantId, item));

                        var color = item.Action switch
                        {
                            "Added" => ConsoleColor.Green,
                            "Dropped" => ConsoleColor.Red,
                            "Modified" => ConsoleColor.Yellow,
                            _ => ConsoleColor.White
                        };

                        Console.ForegroundColor = color;
                        var details = "";
                        if (!string.IsNullOrWhiteSpace(item.OriginalType) || !string.IsNullOrWhiteSpace(item.NewType))
                        {
                            if (!string.IsNullOrWhiteSpace(item.OriginalType) && !string.IsNullOrWhiteSpace(item.NewType))
                                details = $"{item.OriginalType} -> {item.NewType}";
                            else if (!string.IsNullOrWhiteSpace(item.OriginalType))
                                details = $"Old: {item.OriginalType}";
                            else if (!string.IsNullOrWhiteSpace(item.NewType))
                                details = $"New: {item.NewType}";
                        }
                        Console.WriteLine($"{item.Type} | {item.Action} | {item.Table} | {item.Name} | {details}");
                        Console.ResetColor();
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(htmlReportPath))
            {
                GenerateHtmlImpactReport(htmlReportPath, allImpactItems);
            }
        }, tenantOption, assembliesOption, filterTypesOption, htmlReportOption);

        // Add commands
        root.AddCommand(migrateCmd);
        root.AddCommand(bootstrapCmd);
        root.AddCommand(listCmd);
        root.AddCommand(impactCmd);

        return await root.InvokeAsync(args);
    }
}
