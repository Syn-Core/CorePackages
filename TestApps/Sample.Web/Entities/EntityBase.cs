using Syn.Core.MultiTenancy.Context;

namespace Sample.Web.Entities;

public abstract class EntityBase : ITenantEntity
{
    public string? TenantId { get; set; }
}
