using Syn.Core.MultiTenancy.Features.Internal;
using Syn.Core.MultiTenancy.Metadata;

namespace Syn.Core.MultiTenancy.Features
{
    /// <summary>
    /// Reads feature flags from the tenant's metadata dictionary.
    /// Expected metadata key: "features" with comma-separated feature names.
    /// </summary>
    public sealed class MetadataFeatureFlagSource : IFeatureFlagSource
    {
        private readonly ITenantStore _tenantStore;

        public MetadataFeatureFlagSource(ITenantStore tenantStore)
        {
            _tenantStore = tenantStore;
        }

        public Task<IEnumerable<string>> GetFlagsAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            var tenant = _tenantStore.Get(tenantId);
            if (tenant == null)
                return Task.FromResult<IEnumerable<string>>(Array.Empty<string>());

            if (tenant.Metadata.TryGetValue("features", out var features) && !string.IsNullOrWhiteSpace(features))
            {
                var list = features.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return Task.FromResult<IEnumerable<string>>(list);
            }

            return Task.FromResult<IEnumerable<string>>(Array.Empty<string>());
        }
    }
}
