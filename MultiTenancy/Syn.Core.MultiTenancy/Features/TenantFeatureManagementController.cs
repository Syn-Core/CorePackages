using Microsoft.AspNetCore.Mvc;

using Syn.Core.MultiTenancy.Metadata;

namespace Syn.Core.MultiTenancy.Features
{
    [ApiController]
    [Route("api/tenants/{tenantId}/features")]
    public sealed class TenantFeatureManagementController : ControllerBase
    {
        private readonly ITenantStore _tenantStore;

        public TenantFeatureManagementController(ITenantStore tenantStore)
        {
            _tenantStore = tenantStore;
        }

        [HttpGet]
        public IActionResult GetFeatures(string tenantId)
        {
            var tenant = _tenantStore.Get(tenantId);
            if (tenant == null) return NotFound();

            if (tenant.Metadata.TryGetValue("features", out var features))
                return Ok(features.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            return Ok(Array.Empty<string>());
        }

        [HttpPost("{featureName}/enable")]
        public IActionResult EnableFeature(string tenantId, string featureName)
        {
            var tenant = _tenantStore.Get(tenantId);
            if (tenant == null) return NotFound();

            var features = new HashSet<string>(
                tenant.Metadata.TryGetValue("features", out var existing)
                    ? existing.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    : Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            features.Add(featureName);
            tenant.Metadata["features"] = string.Join(",", features);

            _tenantStore.AddOrUpdate(tenant);
            return NoContent();
        }

        [HttpPost("{featureName}/disable")]
        public IActionResult DisableFeature(string tenantId, string featureName)
        {
            var tenant = _tenantStore.Get(tenantId);
            if (tenant == null) return NotFound();

            var features = new HashSet<string>(
                tenant.Metadata.TryGetValue("features", out var existing)
                    ? existing.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    : Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            features.Remove(featureName);
            tenant.Metadata["features"] = string.Join(",", features);

            _tenantStore.AddOrUpdate(tenant);
            return NoContent();
        }
    }
}
