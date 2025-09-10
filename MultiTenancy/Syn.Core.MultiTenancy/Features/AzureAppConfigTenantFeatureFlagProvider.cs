using Syn.Core.MultiTenancy.Features.Internal;

namespace Syn.Core.MultiTenancy.Features
{
    /// <summary>
    /// Tenant-aware feature flag provider that uses AzureAppConfigFeatureFlagSource internally.
    /// </summary>
    public sealed class AzureAppConfigTenantFeatureFlagProvider : ITenantFeatureFlagProvider
    {
        private readonly AzureAppConfigFeatureFlagSource _source;

        /// <summary>
        /// Initializes a new instance for a specific Azure App Configuration connection string.
        /// </summary>
        public AzureAppConfigTenantFeatureFlagProvider(string connectionString)
        {
            _source = new AzureAppConfigFeatureFlagSource(connectionString);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<string>> GetEnabledFeaturesAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            return await _source.GetFlagsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> IsEnabledAsync(string tenantId, string featureName, CancellationToken cancellationToken = default)
        {
            var single = await _source.TryGetFlagAsync(tenantId, featureName, cancellationToken).ConfigureAwait(false);
            if (single.HasValue) return single.Value;

            // Fallback: enumerate all flags (costlier) only if single read wasn’t found.
            var all = await _source.GetFlagsAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return all.Contains(featureName, StringComparer.OrdinalIgnoreCase);
        }
    }
}
