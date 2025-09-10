namespace Syn.Core.MultiTenancy.Features
{
    /// <summary>
    /// Default provider that wraps an internal IFeatureFlagSource.
    /// </summary>
    public sealed class DefaultTenantFeatureFlagProvider : ITenantFeatureFlagProvider
    {
        private readonly Internal.IFeatureFlagSource _source;

        public DefaultTenantFeatureFlagProvider(Internal.IFeatureFlagSource source)
        {
            _source = source;
        }

        public async Task<bool> IsEnabledAsync(string tenantId, string featureName, CancellationToken cancellationToken = default)
        {
            var flags = await _source.GetFlagsAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return flags.Contains(featureName, StringComparer.OrdinalIgnoreCase);
        }

        public Task<IEnumerable<string>> GetEnabledFeaturesAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            return _source.GetFlagsAsync(tenantId, cancellationToken);
        }
    }
}
