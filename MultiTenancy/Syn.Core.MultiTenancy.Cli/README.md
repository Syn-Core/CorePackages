🛠 Multi-Tenancy CLI Tool
A command-line tool for managing a Multi-Tenant environment: running migrations, bootstrapping tenants, listing tenants, and performing Schema Impact Analysis with optional colored HTML reports.

📦 Available Commands
1️⃣ 
Run migrations for all tenants or a specific tenant.
Usage:

Options:
• 	 : Tenant ID or  to run for all tenants (default: ).

2️⃣ 
Add a new tenant and run its migrations.
Usage:

Options:
• 	 : Tenant ID (required).
• 	 : Connection string (required).
• 	 : Schema name (optional).
• 	 : Display name (optional).

3️⃣ 
List all registered tenants.
Usage:


4️⃣ 
Analyze schema changes between the current database state and the expected model.
Usage:

Options:
• 	 : Tenant ID or  (default: ).
• 	 : Comma-separated list of assemblies (paths or names) containing entity types.
• 	 : Comma-separated list of base classes or interfaces to filter entity types (optional).
• 	 : Path to save a colored HTML report (optional).

📂 Passing Assemblies
You can pass assemblies by name or by full path.
| Method | Example | 
| By Name | --entities-assemblies "MyProject.Domain" | 
| By DLL Path | --entities-assemblies "C:\Projects\MySolution\MyProject.Domain.dll" | 
| Multiple Names | --entities-assemblies "MyProject.Domain,MyProject.Shared" | 
| Multiple Paths | --entities-assemblies "C:\Path\MyProject.Domain.dll,C:\Path\MyProject.Shared.dll" | 


Notes:
- If the value ends with .dll, the tool uses Assembly.LoadFrom(path).
- If it does not end with .dll, the tool uses Assembly.Load(name).
- Multiple assemblies are separated by commas.


🎨 Output of impact
Console Output (Color-Coded)
- Added → Green
- Dropped → Red
- Modified → Yellow
Example:
=== Impact Analysis for Tenant: TenantA ===
Column | Added    | Orders | OrderDate
Column | Modified | Orders | CustomerId [int NOT NULL -> bigint NULL]
Index  | Dropped  | Orders | IX_Orders_CustomerId

=== Impact Analysis for Tenant: TenantA ===
Column | Added    | Orders | OrderDate
Column | Modified | Orders | CustomerId [int NOT NULL -> bigint NULL]
Index  | Dropped  | Orders | IX_Orders_CustomerId

(In the console, these lines are color-coded for quick scanning.)

HTML Report Output
When you use --html-report, the tool generates a file like impact-report.html that you can open in any browser.

Color Coding in HTML:
- Added rows have a light green background.
- Dropped rows have a light red background.
- Modified rows have a light yellow background.

📌 Example Runs
Run Impact Analysis for all tenants with HTML report:
dotnet run -- impact --tenant all --entities-assemblies "MyProject.Domain.dll" --html-report "impact.html"


Run Impact Analysis for a single tenant with multiple filter types:
dotnet run -- impact \
  --tenant TenantA \
  --entities-assemblies "MyProject.Domain.dll,MyProject.Shared.dll" \
  --filter-types "MyProject.Domain.IEntity,MyProject.Shared.BaseEntity"



💡 Notes
- If --entities-assemblies is not specified, the tool uses the default assembly.
- If --filter-types is not specified, the tool includes any entity with a [Table] attribute.
- The HTML report is generated only if --html-report is provided.
- You can pass multiple assemblies and multiple filter types, separated by commas.

### 📄 Sample HTML Report
You can view a live example of the HTML report here:  
[View Sample Impact Report](./sample-impact-report.html)