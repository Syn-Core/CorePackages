
namespace Syn.Core.MultiTenancy.Resolution
{
    /// <summary>
    /// Defines a contract for resolving one or more tenant identifiers from a given context.
    /// Supports both single-tenant and multi-tenant resolution strategies.
    /// </summary>
    public interface ITenantResolutionStrategy
    {
        /// <summary>
        /// Attempts to resolve one or more tenant identifiers from the provided context.
        /// </summary>
        /// <param name="context">An object representing the current request or execution context.</param>
        /// <returns>
        /// A collection of resolved tenant identifiers. 
        /// If no tenants are found, returns an empty collection.
        /// </returns>
        IEnumerable<string> ResolveTenantIds(object context);
    }
}