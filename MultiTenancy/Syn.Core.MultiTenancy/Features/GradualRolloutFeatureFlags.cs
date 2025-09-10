namespace Syn.Core.MultiTenancy.Features
{
    /// <summary>
    /// Wraps ITenantFeatureFlags to support gradual rollout based on a hash of a user identifier.
    /// </summary>
    public sealed class GradualRolloutFeatureFlags : ITenantFeatureFlags
    {
        private readonly ITenantFeatureFlags _inner;
        private readonly Dictionary<string, int> _rolloutPercentages; // FeatureName -> %

        public GradualRolloutFeatureFlags(ITenantFeatureFlags inner, Dictionary<string, int> rolloutPercentages)
        {
            _inner = inner;
            _rolloutPercentages = rolloutPercentages;
        }

        public bool IsEnabled(string featureName) => _inner.IsEnabled(featureName);

        public bool IsEnabledForUser(string featureName, string userId)
        {
            if (!_inner.IsEnabled(featureName))
                return false;

            if (!_rolloutPercentages.TryGetValue(featureName, out var percentage) || percentage >= 100)
                return true;

            var hash = Math.Abs(userId.GetHashCode()) % 100;
            return hash < percentage;
        }
    }
}
