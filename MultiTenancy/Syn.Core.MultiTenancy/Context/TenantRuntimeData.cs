using Syn.Core.MultiTenancy.Metadata;

using System.Collections.Concurrent;

namespace Syn.Core.MultiTenancy.Context;

/// <summary>
/// Represents runtime data for a specific tenant.
/// Stores tenant metadata, activation timestamp, and a per-tenant key/value store.
/// </summary>
public class TenantRuntimeData
{
    /// <summary>
    /// Gets or sets the tenant metadata.
    /// </summary>
    public TenantInfo Info { get; set; } = default!;

    /// <summary>
    /// Gets or sets the UTC timestamp when this tenant was activated in the current context.
    /// </summary>
    public DateTime ActivatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the per-tenant key/value store for arbitrary runtime data.
    /// </summary>
    public ConcurrentDictionary<string, object> Items { get; }
        = new(StringComparer.OrdinalIgnoreCase);
}
