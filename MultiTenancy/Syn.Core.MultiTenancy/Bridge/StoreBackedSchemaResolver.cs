using Syn.Core.MultiTenancy.Metadata;
using Syn.Core.MultiTenancy.Resolvers;

namespace Syn.Core.MultiTenancy.Bridge;

/// <summary>
/// Schema resolver backed by ITenantStore.
/// </summary>
public class StoreBackedSchemaResolver : ITenantSchemaResolver
{
    private readonly ITenantStore _store;
    public StoreBackedSchemaResolver(ITenantStore store) => _store = store;

    public string? Resolve(string tenantId)
    {
        var info = _store.GetAsync(tenantId).GetAwaiter().GetResult()
            ?? throw new KeyNotFoundException($"Unknown tenant '{tenantId}'.");
        return info.SchemaName;
    }
}
