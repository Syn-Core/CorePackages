
Multi-Tenancy Architecture Document
1. Overview
This architecture implements a flexible and secure Multi-Tenancy model for .NET applications using EF Core.
It supports both Shared Schema and Schema-per-Tenant approaches, allows multiple tenants per request, enforces strict tenant isolation, and integrates caching for performance optimization.

2. Goals
- Flexibility: Support different tenant resolution strategies (Claims, Headers, Query Strings, etc.).
- Security: Prevent cross-tenant data leakage.
- Performance: Cache tenant metadata to reduce store lookups.
- Extensibility: Allow switching between InMemory, EF Core, or external tenant stores without code changes.
- Developer Experience: Provide defaults that work out-of-the-box in development.

3. Core Components
🔹 Tenant Resolution Strategies
- ClaimTenantResolutionStrategy("tenant_id")
→ Resolves tenant from authenticated user claims.
- HeaderTenantResolutionStrategy("X-Tenant-ID")
→ Resolves tenant from HTTP header.
- QueryStringTenantResolutionStrategy("tenantId")
→ Resolves tenant from query string.
- CompositeTenantResolutionStrategy
→ Combines all strategies in priority order (Claim → Header → QueryString).

🔹 Tenant Metadata & Caching
- ITenantStore
→ Interface for retrieving tenant metadata.
- InMemoryTenantStore
→ Development-only store with static tenant list.
- CachedTenantStore
→ Wraps any ITenantStore with IMemoryCache for performance.
- ITenantCache
→ Interface for cache operations (invalidate, refresh, etc.).

🔹 Tenant Context
- ITenantContext
→ Holds resolved tenants for the current request.
- ActiveTenant → The primary tenant (used for schema, stamping).
- Tenants → List of all allowed tenants (used for filtering).
- ITenantContextAccessor
→ Scoped accessor for retrieving the current context.

🔹 EF Core Integration
- TenantModelCacheKeyFactory
→ Adds schema name to EF Core model cache key for Schema-per-Tenant.
- ApplyEntityDefinitionsToModelMultiTenant
→ Applies query filters based on allowed tenants.
- Uses = for single tenant.
- Uses IN for multiple tenants.
- TenantSaveChangesInterceptor
→ Auto-stamps TenantId on new entities.
→ Enforces tenant isolation on update/delete.

🔹 Configuration & Registration
- MultiTenancyOptions
→ Central configuration object.
- DefaultTenantPropertyName → e.g., "TenantId"
- TenantStoreFactory → Factory for ITenantStore
- TenantIdProviderFactory → Factory for ITenantIdProvider
- UseTenantInterceptor → Enables EF Core enforcement
- AddMultiTenancy()
→ Extension method to register all services.
→ Automatically wires up resolution, context, store, and interceptor.




4. Tenant Resolution Flow
Step-by-step:
- Request Enters → TenantResolutionMiddleware runs.
- Resolution Strategies → Try ClaimTenantResolutionStrategy("tenant_id") → fallback to HeaderTenantResolutionStrategy("X-Tenant-ID") → fallback to QueryStringTenantResolutionStrategy("tenantId").
- Tenant Store Lookup → For each resolved Tenant ID, fetch metadata from ITenantStore (cached).
- Filter Inactive Tenants → Only active tenants are added to ITenantContext.
- Set ActiveTenant → If only one tenant is resolved, set it as ActiveTenant.
- DbContext → Reads ITenantContext to:
- Apply schema (Schema-per-Tenant).
- Apply query filters (Shared Schema).
- EF Core Model Cache → TenantModelCacheKeyFactory ensures separate models per schema.
- SaveChanges → TenantSaveChangesInterceptor enforces tenant isolation.

5. Sequence Diagram (Mermaid)
sequenceDiagram
    participant Client
    participant Middleware as TenantResolutionMiddleware
    participant Strategy as ITenantResolutionStrategy
    participant Store as ITenantStore (Cached)
    participant Context as ITenantContextAccessor
    participant DbContext
    participant EFCore as EF Core

    Client->>Middleware: HTTP Request
    Middleware->>Strategy: Resolve Tenant IDs
    Strategy-->>Middleware: List<TenantId>
    Middleware->>Store: Get TenantInfo for each ID
    Store-->>Middleware: TenantInfo (cached)
    Middleware->>Context: Set MultiTenantContext
    Client->>DbContext: Execute Query
    DbContext->>Context: Get ActiveTenant / Tenants
    DbContext->>EFCore: Apply Schema / Query Filters
    EFCore-->>Client: Tenant-scoped data



6. Deployment Considerations
- Shared Schema: All tenants share the same schema; isolation is enforced via query filters.
- Schema-per-Tenant: Each tenant has its own schema; TenantModelCacheKeyFactory ensures EF Core builds separate models.
- Multiple Tenants per Request: Query filters use IN to allow multiple tenant IDs.
- Caching: CachedTenantStore reduces store lookups; TTL configurable via MultiTenancyOptions.

7. Security
- Default behavior excludes inactive tenants from ITenantContext.
- TenantSaveChangesInterceptor prevents cross-tenant modifications.
- Resolution strategies should be ordered from most secure (Claims) to least secure (QueryString).

8. Extensibility
- Swap ITenantStore implementation without changing business logic.
- Add new resolution strategies (e.g., SubdomainTenantResolutionStrategy).
- Customize MultiTenancyOptions for different environments

## 9. How It Works

The multi-tenancy system operates through a layered pipeline that resolves tenants, enforces isolation, and integrates with EF Core:

1. **Request Enters**  
   The HTTP request is received by the application.

2. **Tenant Resolution Middleware**  
   Executes early in the pipeline. It uses the configured `ITenantResolutionStrategy` to extract one or more Tenant IDs from the request (e.g., from claims, headers, or query string).

3. **Tenant Metadata Lookup**  
   For each resolved Tenant ID, the middleware queries the `ITenantStore` to retrieve metadata (e.g., schema name, connection string).  
   The store is wrapped with `CachedTenantStore` to improve performance.

4. **Tenant Filtering**  
   Inactive tenants are excluded automatically.  
   The remaining tenants are stored in `ITenantContext`, which includes:
   - `Tenants`: list of allowed tenants.
   - `ActiveTenant`: the primary tenant (if only one is resolved).

5. **Tenant Context Access**  
   The `ITenantContextAccessor` provides scoped access to the resolved context for downstream services and DbContexts.

6. **DbContext Behavior**  
   - Reads `ActiveTenant.SchemaName` to set the EF Core schema (for Schema-per-Tenant).
   - Applies query filters using `ApplyEntityDefinitionsToModelMultiTenant` to restrict data access to allowed tenants.

7. **EF Core Model Caching**  
   The `TenantModelCacheKeyFactory` adds the schema name to EF Core's model cache key, ensuring separate models per tenant schema.

8. **SaveChanges Enforcement**  
   The `TenantSaveChangesInterceptor`:
   - Auto-stamps `TenantId` on new entities.
   - Validates that updates/deletes are scoped to allowed tenants.
   - Throws if cross-tenant modification is detected.

9. **Query Execution**  
   EF Core executes queries with tenant filters applied, ensuring strict isolation and correct schema usage.

## 10. Examples

### 10.1 Registering Multi-Tenancy

using Syn.Core.MultiTenancy;

var builder = WebApplication.CreateBuilder(args);

// Add Multi-Tenancy
builder.Services.AddMultiTenancy(options =>
{
    options.DefaultTenantPropertyName = "TenantId";
    options.UseTenantInterceptor = true;

    // Example: Use InMemoryTenantStore with caching
    options.TenantStoreFactory = sp =>
    {
        var cache = sp.GetRequiredService<IMemoryCache>();
        var inner = new InMemoryTenantStore(new List<TenantInfo>
        {
            new TenantInfo("t1", "ConnStr1", "Schema1", "Tenant One", true, DateTime.UtcNow),
            new TenantInfo("t2", "ConnStr2", "Schema2", "Tenant Two", true, DateTime.UtcNow)
        });
        return new CachedTenantStore(inner, cache);
    };
});

var app = builder.Build();

// Use Tenant Resolution Middleware
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapControllers();
app.Run();


### 10.2 Using ITenantContext in a Controller

using Microsoft.AspNetCore.Mvc;
using Syn.Core.MultiTenancy;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ITenantContext _tenantContext;

    public ProductsController(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var activeTenant = _tenantContext.ActiveTenant?.TenantId;
        var allowedTenants = _tenantContext.Tenants.Select(t => t.TenantId);

        return Ok(new
        {
            ActiveTenant = activeTenant,
            AllowedTenants = allowedTenants
        });
    }
}

### 10.3 Configuring DbContext for Multi-Tenancy

using Microsoft.EntityFrameworkCore;
using Syn.Core.MultiTenancy;
using Syn.Core.MultiTenancy.EFCore;

public class AppDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Ensure EF Core builds separate models per schema
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, TenantModelCacheKeyFactory>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply schema if Schema-per-Tenant
        if (!string.IsNullOrWhiteSpace(_tenantContext.ActiveTenant?.SchemaName))
            modelBuilder.HasDefaultSchema(_tenantContext.ActiveTenant.SchemaName);

        // Apply tenant filters for Shared Schema
        var entityTypes = typeof(AppDbContext).Assembly
            .GetTypes()
            .Where(t => modelBuilder.Model.FindEntityType(t) != null);

        modelBuilder.ApplyEntityDefinitionsToModelMultiTenant(entityTypes, _tenantContext);
    }
}

### 10.4 Querying with Tenant Isolation

// Example service method
public async Task<List<Product>> GetProductsAsync(AppDbContext db)
{
    // Query filters are applied automatically
    return await db.Products.ToListAsync();
}

***Key Points:***

- Always register `TenantResolutionMiddleware` early in the request pipeline, ideally right after authentication if tenant resolution depends on user identity.
- Use `ITenantContext` or `ITenantContextAccessor` in services and controllers to access the current tenant(s) without re-resolving them.
- In **Schema-per-Tenant** scenarios:
  - Ensure `TenantModelCacheKeyFactory` is registered so EF Core builds separate models per schema.
  - Set `modelBuilder.HasDefaultSchema()` in `OnModelCreating` based on `ActiveTenant.SchemaName`.
- In **Shared Schema** scenarios:
  - Use `ApplyEntityDefinitionsToModelMultiTenant` to apply global query filters for tenant isolation.
  - Filters will use `=` for a single tenant and `IN` for multiple tenants.
- `TenantSaveChangesInterceptor`:
  - Automatically stamps `TenantId` on new entities if not set.
  - Validates that updates and deletes are scoped to allowed tenants.
  - Throws an exception if a cross-tenant modification is detected.
- `MultiTenancyOptions`:
  - `DefaultTenantPropertyName` should match the property in your entities used for tenant filtering.
  - `TenantStoreFactory` can be swapped to use any store (InMemory, EF Core, Redis, etc.).
  - `TenantIdProviderFactory` can be customized to integrate with your authentication system.
- For performance:
  - Use `CachedTenantStore` to reduce repeated lookups.
  - Configure cache TTL in `CachedTenantStore` constructor.
- For security:
  - Order resolution strategies from most secure (Claims) to least secure (QueryString).
  - Exclude inactive tenants by default in `TenantResolutionMiddleware`.



  📄 MultiTenancyServiceCollectionExtensions – Documentation

  # MultiTenancyServiceCollectionExtensions

## Overview
The `MultiTenancyServiceCollectionExtensions` class provides extension methods for registering multi-tenancy services in the Dependency Injection (DI) container.  
It offers two main registration methods:

1. **`AddMultitenancyCore`** – Registers the essential multi-tenancy services without middleware or EF Core integration.
2. **`AddMultiTenancy`** – Builds on the core registration, adding default store, caching, EF Core interceptor, and optional middleware integration.

---

## 1. AddMultitenancyCore

### Purpose
Registers the **core multi-tenancy services** required for tenant resolution and context management.  
This method is ideal for scenarios where you want to control middleware and EF Core integration manually.

### Signature


public static IServiceCollection AddMultitenancyCore(
    this IServiceCollection services,
    Action<MultiTenancyOptions>? configure = null,
    Func<IServiceProvider, ITenantResolutionStrategy>? strategyFactory = null)

    Parameters
services: The DI service collection.

configure: Optional delegate to configure MultiTenancyOptions.

strategyFactory: Optional factory for creating a custom ITenantResolutionStrategy. If null, a default composite strategy is registered:

ClaimTenantResolutionStrategy("tenant_id")

HeaderTenantResolutionStrategy("X-Tenant-ID")

QueryStringTenantResolutionStrategy("tenantId")

    Behavior
Configures MultiTenancyOptions with provided settings or defaults.

Ensures IMemoryCache is registered.

Registers ITenantContextAccessor for accessing the current tenant context.

Registers the provided ITenantResolutionStrategy or the default composite strategy.

2. AddMultiTenancy
Purpose
Registers the full multi-tenancy setup, including:

Core services from AddMultitenancyCore.

Default tenant store (InMemory + Cached) if not overridden.

Tenant cache exposure via ITenantCache.

EF Core SaveChanges interceptor if enabled in options.

    Signature

public static IServiceCollection AddMultiTenancy(
    this IServiceCollection services,
    Action<MultiTenancyOptions>? configure = null,
    Func<IServiceProvider, ITenantResolutionStrategy>? strategyFactory = null)


    Parameters
services: The DI service collection.

configure: Optional delegate to configure MultiTenancyOptions.

strategyFactory: Optional factory for creating a custom ITenantResolutionStrategy.

    Behavior
Calls AddMultitenancyCore to register core services.

Registers ITenantStore from MultiTenancyOptions.TenantStoreFactory, wrapping it in CachedTenantStore if not already cached.

Registers ITenantCache for direct cache access.

Registers TenantSaveChangesInterceptor if UseTenantInterceptor is true.

3. UseTenantResolution
Purpose
Adds the tenant resolution middleware to the ASP.NET Core request pipeline.

    Signature
public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)

    Parameters
app: The application builder.

Behavior
Executes the configured ITenantResolutionStrategy for each request.

Populates the ITenantContext with resolved tenant(s).

### Example Usage
builder.Services.AddMultiTenancy(options =>
{
    options.DefaultTenantPropertyName = "TenantId";
    options.UseTenantInterceptor = true;
    options.TenantStoreFactory = sp =>
    {
        var cache = sp.GetRequiredService<IMemoryCache>();
        var inner = new InMemoryTenantStore(new List<TenantInfo>
        {
            new TenantInfo("t1", "ConnStr1", "Schema1", "Tenant One", true, DateTime.UtcNow),
            new TenantInfo("t2", "ConnStr2", "Schema2", "Tenant Two", true, DateTime.UtcNow)
        });
        return new CachedTenantStore(inner, cache);
    };
});

var app = builder.Build();
app.UseTenantResolution();

### Key Points
Core vs Full: Use AddMultitenancyCore for minimal setup, AddMultiTenancy for full web integration.

Default Strategy: Claim → Header → QueryString.

Caching: CachedTenantStore improves performance by reducing store lookups.

Security: Order strategies from most secure to least secure.

Extensibility: Swap out store, strategy, or context accessor without changing business logic.

