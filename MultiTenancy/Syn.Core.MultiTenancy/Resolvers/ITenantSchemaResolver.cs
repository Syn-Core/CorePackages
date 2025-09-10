namespace Syn.Core.MultiTenancy.Resolvers
{
    /// <summary>
    /// Resolves the database schema (if any) for a specific tenant.
    /// </summary>
    public interface ITenantSchemaResolver
    {
        /// <summary>
        /// Returns the schema name for the specified tenant. Null means use provider default.
        /// </summary>
        string? Resolve(string tenantId);
    }
}
