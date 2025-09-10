using Syn.Core.MultiTenancy.Metadata;

namespace Syn.Core.MultiTenancy.Resolvers
{
    /// <summary>
    /// Default implementation of <see cref="ITenantSchemaResolver"/>.
    /// This class returns <c>null</c> by default, meaning no schema override will be applied.
    /// 
    /// Replace this with a custom implementation if you are using
    /// the 'Schema per Tenant' multi-tenancy strategy and need to dynamically
    /// determine the schema name for each tenant.
    /// </summary>
    public sealed class DefaultTenantSchemaResolver : ITenantSchemaResolver
    {
        private readonly ITenantStore _store;
        public DefaultTenantSchemaResolver(ITenantStore store) => _store = store;

        public string? Resolve(string tenantId)
        {
            var t = _store.Get(tenantId) ?? throw new InvalidOperationException($"Tenant '{tenantId}' not found.");
            return string.IsNullOrWhiteSpace(t?.SchemaName) ? null : t?.SchemaName;
        }
    }

}
