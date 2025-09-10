using Syn.Core.MultiTenancy.Metadata;
using Syn.Core.MultiTenancy.Resolvers;

namespace Syn.Core.MultiTenancy.Bridge;

/// <summary>
/// Connection string resolver backed by ITenantStore.
/// </summary>
public class StoreBackedConnectionStringResolver : ITenantConnectionStringResolver
{
    private readonly ITenantStore _store;
    public StoreBackedConnectionStringResolver(ITenantStore store) => _store = store;

    public string Resolve(string tenantId)
    {
        var info = _store.GetAsync(tenantId).GetAwaiter().GetResult()
            ?? throw new KeyNotFoundException($"Unknown tenant '{tenantId}'.");
        return info.ConnectionString;
    }
}
