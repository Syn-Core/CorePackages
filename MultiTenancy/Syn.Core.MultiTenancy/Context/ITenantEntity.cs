namespace Syn.Core.MultiTenancy.Context;

/// <summary>
/// Interface for entities that are tenant-aware.
/// </summary>
public interface ITenantEntity
{
    string TenantId { get; set; }
}
