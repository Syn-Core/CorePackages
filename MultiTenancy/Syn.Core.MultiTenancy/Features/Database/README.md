Tenant Feature Flags – EF & SQL Providers
Overview
This library provides multi-tenant feature flag management with two storage options:
- EF Core Provider – Uses any DbContext and integrates with Entity Framework Core.
- SQL Provider – Uses raw ADO.NET (SqlConnection) with a connection string resolver.
Both providers:
- Automatically create the TenantFeatureFlags table at startup using MigrationRunner.
- Support caching and automatic cache invalidation via tenant events.
- Require a list of TenantInfo objects (knownTenants) to run migrations for.

EF Provider
Registration
var tenants = new[]
{
    new TenantInfo("tenant1", configuration.GetConnectionString("Tenant1Db")),
    new TenantInfo("tenant2", configuration.GetConnectionString("Tenant2Db"))
};

services.AddTenantFeatureFlagsEf<MyDbContext>(
    TimeSpan.FromMinutes(5), // Cache duration
    options => options.UseSqlServer(configuration.GetConnectionString("Default")), // DbContext config
    tenants // Known tenants for migration
);



SQL Provider
Registration
var tenants = new[]
{
    new TenantInfo("tenant1", configuration.GetConnectionString("Tenant1Db")),
    new TenantInfo("tenant2", configuration.GetConnectionString("Tenant2Db"))
};

services.AddTenantFeatureFlagsSql(
    tenant => tenant.ConnectionString, // Connection string resolver
    TimeSpan.FromMinutes(5),           // Cache duration
    tenants                            // Known tenants for migration
);




TenantInfo Requirements
TenantInfo must include at least:
- TenantId – Unique identifier for the tenant.
- ConnectionString – Database connection string for that tenant.
Example:
var tenant = new TenantInfo(
    tenantId: "tenant1",
    connectionString: "Server=...;Database=Tenant1;User Id=...;Password=...;"
);



Feature Flag Table
Both providers will ensure the following table exists:
CREATE TABLE TenantFeatureFlags (
    Id INT PRIMARY KEY IDENTITY,
    TenantId NVARCHAR(100) NOT NULL,
    FeatureName NVARCHAR(100) NOT NULL,
    IsEnabled BIT NOT NULL,
    CONSTRAINT UQ_Tenant_Feature UNIQUE (TenantId, FeatureName)
);



Using the Feature Flag Provider
Once registered, you can inject ITenantFeatureFlagProvider into your services or controllers to check and manage feature flags.

Checking if a Feature is Enabled
public class MyService
{
    private readonly ITenantFeatureFlagProvider _featureFlagProvider;

    public MyService(ITenantFeatureFlagProvider featureFlagProvider)
    {
        _featureFlagProvider = featureFlagProvider;
    }

    public async Task DoWorkAsync(string tenantId)
    {
        if (await _featureFlagProvider.IsEnabledAsync(tenantId, "NewDashboard"))
        {
            // Execute code for the new dashboard
        }
        else
        {
            // Fallback to old dashboard
        }
    }
}




Getting All Enabled Features for a Tenant
public class FeatureController : ControllerBase
{
    private readonly ITenantFeatureFlagProvider _featureFlagProvider;

    public FeatureController(ITenantFeatureFlagProvider featureFlagProvider)
    {
        _featureFlagProvider = featureFlagProvider;
    }

    [HttpGet("{tenantId}/features")]
    public async Task<IActionResult> GetFeatures(string tenantId)
    {
        var features = await _featureFlagProvider.GetEnabledFeaturesAsync(tenantId);
        return Ok(features);
    }
}



Managing Feature Flags
Adding a New Feature Flag (EF Example)
public class FeatureFlagAdminService
{
    private readonly MyDbContext _db;

    public FeatureFlagAdminService(MyDbContext db)
    {
        _db = db;
    }

    public async Task AddFeatureFlagAsync(string tenantId, string featureName, bool isEnabled)
    {
        var flag = new TenantFeatureFlag
        {
            TenantId = tenantId,
            FeatureName = featureName,
            IsEnabled = isEnabled
        };

        _db.Set<TenantFeatureFlag>().Add(flag);
        await _db.SaveChangesAsync();
    }
}



Updating an Existing Feature Flag (SQL Example)
public async Task UpdateFeatureFlagAsync(string connectionString, string tenantId, string featureName, bool isEnabled)
{
    using var connection = new SqlConnection(connectionString);
    using var command = new SqlCommand(
        "UPDATE TenantFeatureFlags SET IsEnabled = @IsEnabled WHERE TenantId = @TenantId AND FeatureName = @FeatureName",
        connection);

    command.Parameters.AddWithValue("@IsEnabled", isEnabled);
    command.Parameters.AddWithValue("@TenantId", tenantId);
    command.Parameters.AddWithValue("@FeatureName", featureName);

    await connection.OpenAsync();
    await command.ExecuteNonQueryAsync();
}



Summary
- EF Provider → Use when your project already uses EF Core.
- SQL Provider → Use for lightweight, direct SQL access without EF Core.
- Both require knownTenants to run migrations.
- Both automatically handle caching and cache invalidation.
- After registration, inject ITenantFeatureFlagProvider to check or list features.
- You can manage feature flags directly via EF or SQL depending on your provider.

