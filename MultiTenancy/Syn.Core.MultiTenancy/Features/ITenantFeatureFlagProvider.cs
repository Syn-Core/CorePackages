namespace Syn.Core.MultiTenancy.Features
{
    /// <summary>
    /// Provides feature flag checks for a specific tenant, abstracting the underlying source.
    /// </summary>
    public interface ITenantFeatureFlagProvider
    {
        /// <summary>
        /// Returns true if the specified feature is enabled for the given tenant.
        /// </summary>
        Task<bool> IsEnabledAsync(string tenantId, string featureName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns all enabled features for the given tenant.
        /// </summary>
        Task<IEnumerable<string>> GetEnabledFeaturesAsync(string tenantId, CancellationToken cancellationToken = default);
    }
}
