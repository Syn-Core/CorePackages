namespace Syn.Core.MultiTenancy.Context
{
    /// <summary>
    /// Provides access to the current tenant context.
    /// </summary>
    public interface ITenantContextAccessor
    {
        /// <summary>
        /// Gets or sets the current tenant context.
        /// </summary>
        ITenantContext? TenantContext { get; set; }
    }

}
