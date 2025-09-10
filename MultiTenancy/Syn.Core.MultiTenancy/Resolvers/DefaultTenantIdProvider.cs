namespace Syn.Core.MultiTenancy.Resolvers
{
    /// <summary>
    /// Default implementation of <see cref="ITenantIdProvider"/>.
    /// This class is intended as a placeholder and should be replaced with a custom implementation
    /// that determines the current tenant's ID at runtime.
    /// 
    /// Typical use cases:
    /// - Extracting the tenant ID from the current HTTP request (e.g., from headers or query parameters).
    /// - Reading the tenant ID from a JWT token claim.
    /// - Resolving the tenant ID from the current user's profile or session.
    /// 
    /// Throws <see cref="NotImplementedException"/> by default to ensure
    /// that developers provide their own logic.
    /// </summary>
    public class DefaultTenantIdProvider : ITenantIdProvider
    {
        /// <inheritdoc />
        public string GetCurrentTenantId() =>
            throw new NotImplementedException("Provide your own tenant ID resolution logic.");
    }

}
