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

class Program
{
    static async Task<int> Main(string[] args)
    {
        using var host = Host.CreateDefaultBuilder(args)
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

        var root = new RootCommand("Multi-Tenancy CLI Tool");

        // ===== migrate =====
        var tenantOption = new Option<string>(
            "--tenant",
            () => "all",
            "Tenant ID or 'all' for all tenants"
        );

        var migrateCmd = new Command("migrate", "Run migrations for tenants") { tenantOption };
        migrateCmd.SetHandler(async (string tenantId) =>
        {
            var orchestrator = host.Services.GetRequiredService<TenantMigrationOrchestrator>();
            var entityTypes = typeof(Program).Assembly.GetTypes();

            if (tenantId == "all")
                await orchestrator.MigrateAllTenantsAsync(entityTypes);
            else
                await orchestrator.MigrateTenantAsync(tenantId, entityTypes);
        }, tenantOption);

        // ===== bootstrap =====
        var idOption = new Option<string>("--id", "Tenant ID") { IsRequired = true };
        var connOption = new Option<string>("--conn", "Connection string") { IsRequired = true };
        var schemaOption = new Option<string?>("--schema", "Schema name");
        var displayOption = new Option<string?>("--display", "Display name");

        var bootstrapCmd = new Command("bootstrap", "Bootstrap a new tenant")
        {
            idOption, connOption, schemaOption, displayOption
        };
        bootstrapCmd.SetHandler(async (string id, string conn, string? schema, string? display) =>
        {
            var bootstrapper = host.Services.GetRequiredService<TenantBootstrapper>();
            var entityTypes = typeof(Program).Assembly.GetTypes();
            var tenantInfo = new TenantInfo(id, conn, schema, display);
            await bootstrapper.BootstrapTenantAsync(tenantInfo, entityTypes);
        }, idOption, connOption, schemaOption, displayOption);

        // ===== list =====
        var listCmd = new Command("list", "List all tenants");
        listCmd.SetHandler(async () =>
        {
            var store = host.Services.GetRequiredService<ITenantStore>();
            var tenants = await store.GetAllAsync();
            foreach (var t in tenants)
                Console.WriteLine($"{t.TenantId} | {t.DisplayName} | {t.SchemaName} | Active: {t.IsActive}");
        });

        // ===== impact =====
        var assembliesOption = new Option<string?>(
            "--entities-assemblies",
            "Comma-separated list of assemblies (paths or names) containing entity types"
        );

        var filterTypesOption = new Option<string?>(
            "--filter-types",
            "Comma-separated list of base classes or interfaces to filter entity types (optional)"
        );

        var htmlReportOption = new Option<string?>(
            "--html-report",
            "Path to save HTML report (optional)"
        );

        var impactTenantOption = new Option<string>(
            "--tenant",
            () => "all",
            "Tenant ID or 'all' for all tenants"
        );

        var impactCmd = new Command("impact", "Run schema impact analysis without applying changes")
        {
            impactTenantOption,
            assembliesOption,
            filterTypesOption,
            htmlReportOption
        };

        impactCmd.SetHandler(async (string tenantId, string? assembliesCsv, string? filterTypesCsv, string? htmlReportPath) =>
        {
            var store = host.Services.GetRequiredService<ITenantStore>();
            var tenants = tenantId == "all"
                ? await store.GetAllAsync()
                : new List<TenantInfo> { await store.GetAsync(tenantId) ?? throw new Exception($"Tenant '{tenantId}' not found") };

            // Load assemblies
            var assemblies = new List<System.Reflection.Assembly>();
            if (!string.IsNullOrWhiteSpace(assembliesCsv))
            {
                foreach (var asmNameOrPath in assembliesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    try
                    {
                        assemblies.Add(
                            asmNameOrPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                                ? System.Reflection.Assembly.LoadFrom(asmNameOrPath)
                                : System.Reflection.Assembly.Load(asmNameOrPath)
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Warning: Could not load assembly '{asmNameOrPath}': {ex.Message}");
                    }
                }
            }
            else
            {
                assemblies.Add(typeof(Program).Assembly);
            }

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


            // ===== HTML Report with Tabs =====
            if (!string.IsNullOrWhiteSpace(htmlReportPath))
            {
                // Group items by Type Category
                var groupedByCategory = allImpactItems
                    .GroupBy(x =>
                    {
                        var type = x.Item.Type.ToUpperInvariant();
                        if (type == "COLUMN") return "Columns";
                        if (type.Contains("KEY") || type.Contains("CONSTRAINT")) return "Constraints";
                        if (type == "INDEX") return "Indexes";
                        return "Other";
                    })
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Build summary counts per category and action
                var summaryData = groupedByCategory
                    .ToDictionary(
                        g => g.Key,
                        g => new
                        {
                            Added = g.Value.Count(i => i.Item.Action == "Added"),
                            Dropped = g.Value.Count(i => i.Item.Action == "Dropped"),
                            Modified = g.Value.Count(i => i.Item.Action == "Modified")
                        });

                var html = new System.Text.StringBuilder();
                html.AppendLine("<html><head><meta charset='UTF-8'><title>Impact Analysis Report</title>");
                html.AppendLine("<style>");
                html.AppendLine("body{font-family:Arial, sans-serif;}");
                html.AppendLine(".tab { overflow: hidden; border-bottom: 1px solid #ccc; margin-top: 10px; }");
                html.AppendLine(".tab button { background-color: inherit; border: none; outline: none; cursor: pointer; padding: 10px 16px; transition: 0.3s; font-size: 16px; }");
                html.AppendLine(".tab button:hover { background-color: #ddd; }");
                html.AppendLine(".tab button.active { background-color: #ccc; }");
                html.AppendLine(".tabcontent { display: none; padding: 10px 0; }");
                html.AppendLine("table{border-collapse:collapse;width:100%;margin-top:10px;}");
                html.AppendLine("th,td{border:1px solid #ccc;padding:8px;text-align:left;}");
                html.AppendLine("th{background:#f4f4f4;}");
                html.AppendLine(".Added{background:#d4edda;}");
                html.AppendLine(".Dropped{background:#f8d7da;}");
                html.AppendLine(".Modified{background:#fff3cd;}");
                html.AppendLine("</style>");
                html.AppendLine("<script>");
                html.AppendLine("function openTab(evt, tabName) { var i, tabcontent, tablinks; tabcontent = document.getElementsByClassName('tabcontent'); for (i = 0; i < tabcontent.length; i++) { tabcontent[i].style.display = 'none'; } tablinks = document.getElementsByClassName('tablinks'); for (i = 0; i < tablinks.length; i++) { tablinks[i].className = tablinks[i].className.replace(' active', ''); } document.getElementById(tabName).style.display = 'block'; if (evt) evt.currentTarget.className += ' active'; }");
                html.AppendLine("</script>");
                html.AppendLine("</head><body>");
                html.AppendLine("<h1>Impact Analysis Report</h1>");

                // ===== Summary Table =====
                html.AppendLine("<h2>Summary</h2>");
                html.AppendLine("<table>");
                html.AppendLine("<tr><th>Category</th><th>Added</th><th>Dropped</th><th>Modified</th><th>Total</th></tr>");
                foreach (var kvp in summaryData)
                {
                    var total = kvp.Value.Added + kvp.Value.Dropped + kvp.Value.Modified;
                    html.AppendLine($"<tr>" +
                        $"<td>{System.Net.WebUtility.HtmlEncode(kvp.Key)}</td>" +
                        $"<td class='Added'>{kvp.Value.Added}</td>" +
                        $"<td class='Dropped'>{kvp.Value.Dropped}</td>" +
                        $"<td class='Modified'>{kvp.Value.Modified}</td>" +
                        $"<td>{total}</td>" +
                        $"</tr>");
                }
                html.AppendLine("</table>");

                // ===== Tabs =====
                html.AppendLine("<div class='tab'>");
                foreach (var category in groupedByCategory.Keys)
                {
                    html.AppendLine($"<button class='tablinks' onclick=\"openTab(event, '{category}')\">{System.Net.WebUtility.HtmlEncode(category)}</button>");
                }
                html.AppendLine("</div>");

                // Tab contents
                foreach (var category in groupedByCategory)
                {
                    html.AppendLine($"<div id='{System.Net.WebUtility.HtmlEncode(category.Key)}' class='tabcontent'>");
                    html.AppendLine("<table>");
                    html.AppendLine("<tr><th>Tenant</th><th>Type</th><th>Action</th><th>Table</th><th>Name</th><th>Details</th></tr>");

                    foreach (var (Tenant, Item) in category.Value)
                    {
                        string details = "";
                        if (!string.IsNullOrWhiteSpace(Item.OriginalType) || !string.IsNullOrWhiteSpace(Item.NewType))
                        {
                            if (!string.IsNullOrWhiteSpace(Item.OriginalType) && !string.IsNullOrWhiteSpace(Item.NewType))
                                details = $"{System.Net.WebUtility.HtmlEncode(Item.OriginalType)} → {System.Net.WebUtility.HtmlEncode(Item.NewType)}";
                            else if (!string.IsNullOrWhiteSpace(Item.OriginalType))
                                details = $"Old: {System.Net.WebUtility.HtmlEncode(Item.OriginalType)}";
                            else if (!string.IsNullOrWhiteSpace(Item.NewType))
                                details = $"New: {System.Net.WebUtility.HtmlEncode(Item.NewType)}";
                        }

                        html.AppendLine($"<tr class='{System.Net.WebUtility.HtmlEncode(Item.Action)}'>" +
                            $"<td>{System.Net.WebUtility.HtmlEncode(Tenant)}</td>" +
                            $"<td>{System.Net.WebUtility.HtmlEncode(Item.Type)}</td>" +
                            $"<td>{System.Net.WebUtility.HtmlEncode(Item.Action)}</td>" +
                            $"<td>{System.Net.WebUtility.HtmlEncode(Item.Table)}</td>" +
                            $"<td>{System.Net.WebUtility.HtmlEncode(Item.Name)}</td>" +
                            $"<td>{details}</td>" +
                            "</tr>");
                    }

                    html.AppendLine("</table>");
                    html.AppendLine("</div>");
                }

                // Auto open first tab
                html.AppendLine("<script>var firstTab = document.getElementsByClassName('tablinks')[0]; if(firstTab){ firstTab.className += ' active'; openTab(null, firstTab.textContent); }</script>");
                html.AppendLine("</body></html>");

                try
                {
                    System.IO.File.WriteAllText(htmlReportPath, html.ToString());
                    Console.WriteLine($"\nHTML report saved to: {htmlReportPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to save HTML report: {ex.Message}");
                }
            }
        }, impactTenantOption, assembliesOption, filterTypesOption, htmlReportOption);

        // Add commands
        root.AddCommand(migrateCmd);
        root.AddCommand(bootstrapCmd);
        root.AddCommand(listCmd);
        root.AddCommand(impactCmd);

        return await root.InvokeAsync(args);
    }
}
