using Syn.Core.MultiTenancy.Metadata;

using System.Collections.Concurrent;

namespace Syn.Core.MultiTenancy.Context;
/// <summary>
/// Default implementation of <see cref="ITenantContext"/> that supports both single-tenant
/// and multi-tenant scenarios, with per-tenant runtime data storage.
/// </summary>
/// <remarks>
/// This context stores:
/// - A collection of tenants available to the current user.
/// - An active tenant for operations requiring a single tenant scope.
/// - A thread-safe per-tenant runtime data store containing tenant metadata and arbitrary data.
/// 
/// The internal data store is private to ensure isolation and prevent
/// external code from accessing all tenants' data at once.
/// </remarks>
/// <summary>
/// Default implementation of <see cref="ITenantContext"/> that supports both single-tenant
/// and multi-tenant scenarios, with per-tenant runtime data storage.
/// </summary>
/// <remarks>
/// This context stores:
/// - A collection of tenants available to the current user.
/// - An active tenant for operations requiring a single tenant scope.
/// - A thread-safe per-tenant runtime data store containing tenant metadata and arbitrary data.
/// 
/// The internal data store is private to ensure isolation and prevent
/// external code from accessing all tenants' data at once.
/// </remarks>
internal class MultiTenantContext : ITenantContext
{
    private TenantInfo? _activeTenant;

    /// <summary>
    /// Thread-safe store for per-tenant runtime data.
    /// Key = TenantId, Value = <see cref="TenantRuntimeData"/>
    /// </summary>
    private readonly ConcurrentDictionary<string, TenantRuntimeData> _tenantDataCache =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<TenantInfo> Tenants { get; } = [];
    public TenantInfo? ActiveTenant => _activeTenant;
    public string TenantPropertyName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MultiTenantContext"/> class.
    /// </summary>
    public MultiTenantContext(IEnumerable<TenantInfo> tenants, string? tenantPropertyName = default)
    {
        if (tenants == null) throw new ArgumentNullException(nameof(tenants));

        Tenants = tenants.ToList() ?? [];
        TenantPropertyName = tenantPropertyName ?? MultiTenancyOptions.Instance.DefaultTenantPropertyName;
        _activeTenant = Tenants.FirstOrDefault();

        // Initialize runtime data for the default active tenant if present
        if (_activeTenant != null)
        {
            _tenantDataCache.TryAdd(_activeTenant.TenantId, new TenantRuntimeData
            {
                Info = _activeTenant,
                ActivatedAt = DateTime.UtcNow
            });
        }
    }

    /// <inheritdoc />
    public bool SetActiveTenant(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return false;

        var tenant = Tenants.FirstOrDefault(t =>
            t.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase));

        if (tenant != null)
        {
            _activeTenant = tenant;

            // Initialize runtime data for the tenant if not already present
            _tenantDataCache.GetOrAdd(tenantId, _ =>
            {
                Console.WriteLine($"[TRACE] Initializing runtime data for tenant: {tenantId}");
                return new TenantRuntimeData
                {
                    Info = tenant,
                    ActivatedAt = DateTime.UtcNow
                };
            });

            return true;
        }

        return false;
    }

    /// <summary>
    /// Stores or updates multiple key/value pairs for a specific tenant's runtime data.
    /// If the tenant does not exist in the cache, it will be created automatically.
    /// </summary>
    public void SetTenantData(string tenantId, Dictionary<string, object>? data)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentNullException(nameof(tenantId));

        var runtimeData = _tenantDataCache.GetOrAdd(tenantId, id => new TenantRuntimeData
        {
            Info = Tenants.FirstOrDefault(t => t.TenantId.Equals(id, StringComparison.OrdinalIgnoreCase))
                   ?? new TenantInfo(id, string.Empty),
            ActivatedAt = DateTime.UtcNow
        });

        if (data != null)
        {
            foreach (var kvp in data)
            {
                runtimeData.Items.AddOrUpdate(
                    kvp.Key,
                    kvp.Value,
                    (key, oldValue) => kvp.Value
                );
            }
        }
    }

    /// <summary>
    /// Stores or replaces the full runtime data object for a specific tenant.
    /// </summary>
    public void SetTenantData(string tenantId, TenantRuntimeData data)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentNullException(nameof(tenantId));

        if (data == null)
            throw new ArgumentNullException(nameof(data));

        _tenantDataCache[tenantId] = data;
    }

    /// <summary>
    /// Retrieves the full runtime data object for a specific tenant.
    /// </summary>
    public TenantRuntimeData? GetTenantData(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentNullException(nameof(tenantId));

        _tenantDataCache.TryGetValue(tenantId, out var runtimeData);
        return runtimeData;
    }

    /// <summary>
    /// Retrieves the full runtime data object for a specific tenant,
    /// or creates and stores it if it does not exist.
    /// </summary>
    public TenantRuntimeData GetOrAddTenantData(string tenantId, Func<string, TenantRuntimeData> factory)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentNullException(nameof(tenantId));

        if (factory == null)
            throw new ArgumentNullException(nameof(factory));

        return _tenantDataCache.GetOrAdd(tenantId, factory);
    }
}
