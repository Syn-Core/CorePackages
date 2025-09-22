using Microsoft.Extensions.Caching.Memory;

using Syn.Core.MultiTenancy.Metadata;

using System.Collections.Concurrent;

namespace Syn.Core.MultiTenancy.Context;

/// <summary>
/// Represents the tenant context for the current request or operation.
/// Supports both single-tenant and multi-tenant scenarios.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Gets the list of tenants available to the current user.
    /// In a single-tenant scenario, this list will contain only one tenant.
    /// </summary>
    IReadOnlyList<TenantInfo> Tenants { get; }

    /// <summary>
    /// Gets or sets the currently active tenant.
    /// This is useful for operations that require a single tenant scope.
    /// </summary>
    TenantInfo? ActiveTenant { get; }

    /// <summary>
    /// Gets the default tenant property name (e.g., "TenantId") used for filtering or identification.
    /// </summary>
    string TenantPropertyName { get; }

    /// <summary>
    /// Sets the active tenant by its identifier, if it exists in the available tenants list.
    /// </summary>
    /// <param name="tenantId">The tenant identifier to set as active.</param>
    /// <returns>
    /// <c>true</c> if the active tenant was successfully set; otherwise, <c>false</c>.
    /// </returns>
    bool SetActiveTenant(string tenantId);


    /// <summary>
    /// Stores or updates multiple key/value pairs for a specific tenant's runtime data.
    /// If the tenant does not exist in the cache, it will be created automatically.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="data">A dictionary of key/value pairs to store or update.</param>
    /// <exception cref="ArgumentNullException">Thrown if tenantId or data is null/empty.</exception>
    void SetTenantData(string tenantId, Dictionary<string, object>? data);

    /// <summary>
    /// Retrieves the full runtime data object for a specific tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>
    /// The <see cref="TenantRuntimeData"/> for the specified tenant,
    /// or null if the tenant does not exist in the cache.
    /// </returns>
    TenantRuntimeData? GetTenantData(string tenantId);

    /// <summary>
    /// Retrieves the full runtime data object for a specific tenant,
    /// or creates and stores it if it does not exist.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="factory">
    /// A factory function to create the <see cref="TenantRuntimeData"/> if it does not exist.
    /// </param>
    /// <returns>The existing or newly created <see cref="TenantRuntimeData"/>.</returns>
    public TenantRuntimeData GetOrAddTenantData(string tenantId, Func<string, TenantRuntimeData> factory);

}