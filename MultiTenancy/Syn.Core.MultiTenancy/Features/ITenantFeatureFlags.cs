namespace Syn.Core.MultiTenancy.Features
{
    /// <summary>
    /// Abstraction for providing feature flags for a tenant.
    /// </summary>
    public interface ITenantFeatureFlags
    {
        /// <summary>
        /// Returns true if the specified feature is enabled for the tenant.
        /// </summary>
        bool IsEnabled(string featureName);
    }
}
