namespace Syn.Core.MultiTenancy.Features
{
    /// <summary>
    /// Default in-memory feature flag implementation.
    /// </summary>
    public sealed class TenantFeatureFlags : ITenantFeatureFlags
    {
        private readonly ISet<string> _enabled;

        public TenantFeatureFlags(IEnumerable<string> enabledFeatures)
        {
            _enabled = new HashSet<string>(enabledFeatures ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        }

        public bool IsEnabled(string featureName) => _enabled.Contains(featureName);
    }
}
