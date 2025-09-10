using Microsoft.EntityFrameworkCore;

namespace Syn.Core.MultiTenancy.EFCore
{
    /// <summary>
    /// Provides a DbContext instance configured for a given tenant, including connection and schema.
    /// </summary>
    public interface ITenantDbContextFactory
    {
        /// <summary>
        /// Builds a DbContext instance for the tenant, covering the supplied entity types.
        /// </summary>
        DbContext Create(string tenantId, IEnumerable<Type> entityTypes);
    }
}
