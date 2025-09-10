namespace Syn.Core.MultiTenancy.Context
{
    /// <summary>
    /// Default accessor for the current ITenantContext.
    /// </summary>
    public class TenantContextAccessor : ITenantContextAccessor
    {
        /// <inheritdoc />
        public ITenantContext? TenantContext { get; set; }
    }

}
