using Syn.Core.MultiTenancy.Metadata;

/// <summary>
/// An optional decorator interface for caching tenant metadata.
/// </summary>
public interface ITenantCache
{
    Task<TenantInfo?> GetAsync(string tenantId, Func<Task<TenantInfo?>> factory, CancellationToken ct = default);
    Task InvalidateAsync(string tenantId, CancellationToken ct = default);
    Task InvalidateAllAsync(CancellationToken ct = default);
}