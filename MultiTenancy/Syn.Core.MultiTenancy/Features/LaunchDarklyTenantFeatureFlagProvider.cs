using Syn.Core.MultiTenancy.Features.Internal;

namespace Syn.Core.MultiTenancy.Features
{
    /// <summary>
    /// Tenant-aware feature flag provider that uses LaunchDarklyFeatureFlagSource internally.
    /// </summary>
    public sealed class LaunchDarklyTenantFeatureFlagProvider : ITenantFeatureFlagProvider, IDisposable
    {
        private readonly LaunchDarklyFeatureFlagSource _source;

        public LaunchDarklyTenantFeatureFlagProvider(string sdkKey)
        {
            _source = new LaunchDarklyFeatureFlagSource(sdkKey);
        }

        public async Task<IEnumerable<string>> GetEnabledFeaturesAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            return await _source.GetFlagsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        }

        public async Task<bool> IsEnabledAsync(string tenantId, string featureName, CancellationToken cancellationToken = default)
        {
            var flags = await _source.GetFlagsAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return flags.Contains(featureName, StringComparer.OrdinalIgnoreCase);
        }

        public void Dispose() => _source.Dispose();
    }
}
