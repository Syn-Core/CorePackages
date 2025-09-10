using Syn.Core.MultiTenancy.Features.Internal;

namespace Syn.Core.MultiTenancy.Features
{
    /// <summary>
    /// Simple in-memory feature flag source for testing or non-DB scenarios.
    /// </summary>
    public sealed class InMemoryFeatureFlagSource : IFeatureFlagSource
    {
        private readonly Dictionary<string, List<string>> _flagsByTenant = new(StringComparer.OrdinalIgnoreCase)
        {
            { "tenant1", new List<string> { "FeatureA", "FeatureB" } },
            { "tenant2", new List<string> { "FeatureB" } }
        };

        public Task<IEnumerable<string>> GetFlagsAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (_flagsByTenant.TryGetValue(tenantId, out var flags))
                return Task.FromResult<IEnumerable<string>>(flags);

            return Task.FromResult<IEnumerable<string>>(Array.Empty<string>());
        }
    }
}
