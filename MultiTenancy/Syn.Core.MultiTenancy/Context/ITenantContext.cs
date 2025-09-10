using Syn.Core.MultiTenancy.Metadata;

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
    TenantInfo? ActiveTenant { get; set; }

    /// <summary>
    /// Gets the default tenant property name (e.g., "TenantId") used for filtering or identification.
    /// </summary>
    string TenantPropertyName { get; }

}