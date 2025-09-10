namespace Syn.Core.MultiTenancy.DI
{
    /// <summary>
    /// Ambient context available during tenant container build.
    /// </summary>
    public sealed class TenantContainerBuildContext
    {
        /// <summary>
        /// Initializes a new instance with the provided tenant identifier.
        /// </summary>
        public TenantContainerBuildContext(string tenantId)
        {
            TenantId = tenantId;
        }

        /// <summary>
        /// The tenant identifier for the container being built.
        /// </summary>
        public string TenantId { get; }
    }
}
