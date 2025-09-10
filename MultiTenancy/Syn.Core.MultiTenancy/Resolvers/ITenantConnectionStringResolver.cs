namespace Syn.Core.MultiTenancy.Resolvers
{
    /// <summary>
    /// Resolves a connection string for a specific tenant.
    /// </summary>
    public interface ITenantConnectionStringResolver
    {
        /// <summary>
        /// Returns the connection string for the specified tenant.
        /// </summary>
        string Resolve(string tenantId);
    }
}
