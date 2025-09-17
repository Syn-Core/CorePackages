using Microsoft.Extensions.Hosting;

using Syn.Core.SqlSchemaGenerator;

using System.Reflection;

partial class Program
{
    // ===== Helper: Try to get ServiceProvider from Startup Project =====
    private static IServiceProvider? TryGetStartupServiceProvider(string? startupAssemblyNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(startupAssemblyNameOrPath))
            return null;

        try
        {
            Assembly startupAssembly;

            // Load by path or by name
            if (startupAssemblyNameOrPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                startupAssembly = Assembly.LoadFrom(startupAssemblyNameOrPath);
            else
                startupAssembly = Assembly.Load(startupAssemblyNameOrPath);

            // Try to find a type named "Program"
            var programType = startupAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "Program");

            if (programType != null)
            {
                // Look for a static method returning IHost
                var buildHostMethod = programType.GetMethod("BuildHost", BindingFlags.Public | BindingFlags.Static);
                if (buildHostMethod != null)
                {
                    var host = buildHostMethod.Invoke(null, null) as IHost;
                    return host?.Services;
                }

                // Or a static property returning IServiceProvider
                var serviceProviderProp = programType.GetProperty("ServiceProvider", BindingFlags.Public | BindingFlags.Static);
                if (serviceProviderProp != null)
                {
                    return serviceProviderProp.GetValue(null) as IServiceProvider;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Could not load ServiceProvider from startup assembly: {ex.Message}");
        }

        return null;
    }


    // ===== Helper: Load Assemblies =====
    private static List<Assembly> LoadAssemblies(string? assembliesCsv)
    {
        var assemblies = new List<Assembly>();

        if (!string.IsNullOrWhiteSpace(assembliesCsv))
        {
            foreach (var asmNameOrPath in assembliesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                try
                {
                    assemblies.Add(
                        asmNameOrPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                            ? Assembly.LoadFrom(asmNameOrPath)
                            : Assembly.Load(asmNameOrPath)
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
            // Default fallback: CLI assembly itself
            assemblies.Add(typeof(Program).Assembly);
        }

        return assemblies;
    }


    internal static void GenerateHtmlImpactReport(
            string htmlReportPath,
            List<(string Tenant, ImpactItem Item)> allImpactItems)
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

        // Summary Table
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

        // Tabs
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

}
