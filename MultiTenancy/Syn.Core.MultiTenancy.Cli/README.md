# 🛠 Multi-Tenancy CLI Tool

A command-line tool for managing a **Multi-Tenant** environment:  
- Running migrations  
- Bootstrapping tenants  
- Listing tenants  
- Performing **Schema Impact Analysis** with optional **color-coded HTML reports**

## 🚀 How to Run the CLI

The Multi-Tenancy CLI is a **local project** in your solution, not a globally installed tool.  
You can run it in two main ways:

---

### 1️⃣ From Inside the CLI Project Folder
If your terminal is already in the `Syn.Core.MultiTenancy.Cli` folder (where its `Program.cs` is located):

```bash
dotnet run -- <command> [options]
```

Example:

```bash
dotnet run -- migrate --tenant all --entities-assemblies "MyProject.Domain.dll" --startup-assembly "MyProject.Startup"
```

### 2️⃣ From Anywhere in the Solution (Specify the Project)
If you are in the solution root or any other folder, use the --project option to point to the CLI project:

```bash
dotnet run --project path/to/Syn.Core.MultiTenancy.Cli -- <command> [options]
```

### Example:

```bash
dotnet run --project ./MultiTenancy/Syn.Core.MultiTenancy.Cli -- impact \
  --tenant all \
  --entities-assemblies "MyProject.Domain.dll" \
  --html-report "impact.html" \
  --startup-assembly "C:\Projects\MySolution\MyProject.Startup\bin\Debug\net8.0\MyProject.Startup.dll"
```

## 💡 Notes
Everything after -- is passed to the CLI itself, not to dotnet run.

--startup-assembly can be either the assembly name or the full DLL path of your Startup Project.

If --startup-assembly is omitted, the CLI will build its own internal host with default service registrations.

In CI/CD pipelines or scripts, always use the --project form to avoid depending on the current working directory.


### 📦 Available Commands


### 🔹 Common Option: --startup-assembly
Specifies the startup assembly (by name or DLL path) containing `Program.cs` for the main application.  
If provided, the CLI will attempt to load the `IServiceProvider` from this assembly instead of building its own internal host.

**Usage Examples:**
- By name:  
```bash
  --startup-assembly "MyApp.Startup"
```

By DLL path:

```bash
--startup-assembly "C:\Projects\MyApp\bin\Debug\net8.0\MyApp.Startup.dll"
```

Notes:

If the value ends with .dll, the tool uses Assembly.LoadFrom(path).

If it does not end with .dll, the tool uses Assembly.Load(name).

If not provided, the CLI will build an internal host with default service registrations.

----

## 📦 Available Commands

### 1️⃣ Run Migrations
Run migrations for **all tenants** or a **specific tenant**.

**Usage:**
```bash
dotnet run -- migrate --tenant <TenantId|all> [--entities-assemblies "..."]
```

Options:

--tenant : Tenant ID or all to run for all tenants (default: all).

--entities-assemblies : Comma-separated list of assemblies containing entity types.

2️⃣ Bootstrap a New Tenant
Add a new tenant and run its migrations.

Usage:

```bash
dotnet run -- bootstrap --id <TenantId> --conn "<ConnectionString>" [--schema "<SchemaName>"] [--display "<DisplayName>"] [--entities-assemblies "..."]
```
Options:

--id : Tenant ID (required)

--conn : Connection string (required)

--schema : Schema name (optional)

--display : Display name (optional)

--entities-assemblies : Comma-separated list of assemblies containing entity types.

3️⃣ List Tenants
List all registered tenants.

Usage:

```bash
dotnet run -- list
```

4️⃣ Schema Impact Analysis
Analyze schema changes between the current database state and the expected model.

```Usage:

bash
dotnet run -- impact --tenant <TenantId|all> --entities-assemblies "..." [--filter-types "..."] [--html-report "path.html"]
```

Options:

--tenant : Tenant ID or all (default: all)

--entities-assemblies : Comma-separated list of assemblies (paths or names) containing entity types

--filter-types : Comma-separated list of base classes or interfaces to filter entity types (optional)

--html-report : Path to save a colored HTML report (optional)

📂 Passing Assemblies
You can pass assemblies by name or by full path:

Method				Example
By Name				--entities-assemblies "MyProject.Domain"
By DLL Path			--entities-assemblies "C:\Projects\MySolution\MyProject.Domain.dll"
Multiple Names		--entities-assemblies "MyProject.Domain,MyProject.Shared"
Multiple Paths		--entities-assemblies "C:\Path\MyProject.Domain.dll,C:\Path\MyProject.Shared.dll"

Notes:

If the value ends with .dll, the tool uses Assembly.LoadFrom(path).

If it does not end with .dll, the tool uses Assembly.Load(name).

Multiple assemblies are separated by commas.

🎨 Output of impact
Console Output (Color-Coded)
Added → Green

Dropped → Red

Modified → Yellow

Example:

Code
=== Impact Analysis for Tenant: TenantA ===
Column | Added    | Orders | OrderDate
Column | Modified | Orders | CustomerId [int NOT NULL -> bigint NULL]
Index  | Dropped  | Orders | IX_Orders_CustomerId
(In the console, these lines are color-coded for quick scanning.)

HTML Report Output
When you use --html-report, the tool generates a file (e.g., impact-report.html) that you can open in any browser.

Color Coding in HTML:

Added rows → Light green background

Dropped rows → Light red background

Modified rows → Light yellow background

📌 Example Runs
Run Impact Analysis for all tenants with HTML report:

```bash
dotnet run -- impact --tenant all --entities-assemblies "MyProject.Domain.dll" --html-report "impact.html"
```

Run Impact Analysis for a single tenant with multiple filter types:

```bash
dotnet run -- impact \
  --tenant TenantA \
  --entities-assemblies "MyProject.Domain.dll,MyProject.Shared.dll" \
  --filter-types "MyProject.Domain.IEntity,MyProject.Shared.BaseEntity"
  ```

💡 Notes
If --entities-assemblies is not specified, the tool uses the default assembly.

If --filter-types is not specified, the tool includes any entity with a [Table] attribute.

The HTML report is generated only if --html-report is provided.

You can pass multiple assemblies and multiple filter types, separated by commas.
If --startup-assembly is not specified, the CLI will build its own internal host.


### 📄 Sample HTML Report
You can view a live example of the HTML report here:  
[View Sample Impact Report](./sample-impact-report.html)


## 🗂 Multi-Tenancy CLI Tool - Command Flow

```mermaid
flowchart TD
    A[User runs CLI command] --> B{Which command?}

    %% Migrate Flow
    B -- migrate --> M1[Parse --tenant & --entities-assemblies]
    M1 --> M2[Load assemblies & entity types]
    M2 --> M3[Resolve TenantMigrationOrchestrator]
    M3 --> M4{tenant == all?}
    M4 -- Yes --> M5[MigrateAllTenantsAsync(entityTypes)]
    M4 -- No --> M6[MigrateTenantAsync(tenantId, entityTypes)]
    M5 --> Z[Done]
    M6 --> Z

    %% Bootstrap Flow
    B -- bootstrap --> B1[Parse --id, --conn, --schema, --display, --entities-assemblies]
    B1 --> B2[Load assemblies & entity types]
    B2 --> B3[Resolve TenantBootstrapper]
    B3 --> B4[Create TenantInfo]
    B4 --> B5[BootstrapTenantAsync(tenantInfo, entityTypes)]
    B5 --> Z

    %% List Flow
    B -- list --> L1[Resolve ITenantStore]
    L1 --> L2[GetAllAsync()]
    L2 --> L3[Print tenant list to console]
    L3 --> Z

    %% Impact Flow
    B -- impact --> I1[Parse --tenant, --entities-assemblies, --filter-types, --html-report]
    I1 --> I2[Load assemblies & filter types]
    I2 --> I3[Resolve ITenantStore & tenants]
    I3 --> I4[Build new schema from entity types]
    I4 --> I5[Read old schema from DB]
    I5 --> I6[AnalyzeImpact(oldSchema, newSchema)]
    I6 --> I7[Print color-coded results to console]
    I7 --> I8{--html-report provided?}
    I8 -- Yes --> I9[Generate HTML report]
    I8 -- No --> Z
    I9 --> Z

    Z[End]
```

## 🔄 ServiceProvider Resolution Flow

```mermaid
flowchart TD
    A[Start CLI Execution] --> B[Check --startup-assembly option]
    B -- Not Provided --> F[Build Internal CLI Host] --> Z[Use Internal ServiceProvider]
    B -- Provided --> C[Try Load Startup Assembly (by name or path)]
    C --> D{Assembly Loaded?}
    D -- No --> F
    D -- Yes --> E[Search for Program type]
    E --> G{Found BuildHost() or ServiceProvider?}
    G -- Yes --> H[Invoke Method/Property to get ServiceProvider] --> Z[Use Startup ServiceProvider]
    G -- No --> F
```



🔹 Example CLI Commands with --startup-assembly
1️⃣ Run Migrations
Run migrations for all tenants (or a specific tenant) using the ServiceProvider from your Startup Project:

```bash
dotnet run --project Syn.Core.MultiTenancy.Cli -- migrate \
  --tenant all \
  --entities-assemblies "MyProject.Domain.dll,MyProject.Shared.dll" \
  --startup-assembly "C:\Projects\MySolution\MyProject.Startup\bin\Debug\net8.0\MyProject.Startup.dll"
```

2️⃣ Bootstrap a New Tenant
Add a new tenant and run its migrations:

```bash
dotnet run --project Syn.Core.MultiTenancy.Cli -- bootstrap \
  --id TenantX \
  --conn "Server=.;Database=TenantXDb;Trusted_Connection=True;" \
  --schema TenantXSchema \
  --display "Tenant X Display Name" \
  --entities-assemblies "MyProject.Domain.dll" \
  --startup-assembly "MyProject.Startup"
```

3️⃣ List Tenants
List all registered tenants:

```bash
dotnet run --project Syn.Core.MultiTenancy.Cli -- list \
  --startup-assembly "MyProject.Startup"
```

4️⃣ Schema Impact Analysis
Analyze schema differences between the current database and the expected model, with an HTML report:

```bash
dotnet run --project Syn.Core.MultiTenancy.Cli -- impact \
  --tenant TenantA \
  --entities-assemblies "MyProject.Domain.dll,MyProject.Shared.dll" \
  --filter-types "MyProject.Domain.IEntity,MyProject.Shared.BaseEntity" \
  --html-report "impact.html" \
  --startup-assembly "C:\Projects\MySolution\MyProject.Startup\bin\Debug\net8.0\MyProject.Startup.dll"
```

💡 Notes
--startup-assembly can be either the assembly name or the full DLL path of your Startup Project.

If the value ends with .dll, the tool uses Assembly.LoadFrom(path).

If it does not end with .dll, the tool uses Assembly.Load(name).

If omitted, the CLI will build its own internal host with default service registrations.