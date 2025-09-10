using Syn.Core.MultiTenancy.Metadata;

namespace Syn.Core.MultiTenancy.Resolvers
{
    /// <summary>
    /// Default implementation of <see cref="ITenantConnectionStringResolver"/>.
    /// This class is intended as a placeholder and should be replaced with a custom implementation
    /// that retrieves the connection string for a given tenant.
    /// 
    /// Typical use cases:
    /// - Fetching connection strings from a configuration file.
    /// - Retrieving connection strings from a secure secrets store (e.g., Azure Key Vault).
    /// - Querying a master database that stores tenant metadata.
    /// 
    /// Throws <see cref="NotImplementedException"/> by default to ensure
    /// that developers provide their own logic.
    /// </summary>
    public class DefaultTenantConnectionStringResolver : ITenantConnectionStringResolver
    {
        /// <inheritdoc />
        private readonly ITenantStore _store;

        public DefaultTenantConnectionStringResolver(ITenantStore store)
        {
            _store = store;
        }

        public string Resolve(string tenantId)
        {
            var t = _store.Get(tenantId) ?? throw new InvalidOperationException($"Tenant '{tenantId}' not found.");
            if (string.IsNullOrWhiteSpace(t.ConnectionString))
                throw new InvalidOperationException($"Tenant '{tenantId}' has no connection string.");
            return t.ConnectionString!;
        }
    }

}
