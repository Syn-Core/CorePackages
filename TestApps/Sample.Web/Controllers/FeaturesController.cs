using Microsoft.AspNetCore.Mvc;

using Syn.Core.MultiTenancy.Features;

namespace Sample.Web.Controllers
{
    /// <summary>
    /// Provides endpoints to query enabled features for tenants.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [ApiExplorerSettings(GroupName = "Features")]
    public class FeaturesController : ControllerBase
    {
        private readonly ITenantFeatureFlagProvider _provider;

        public FeaturesController(ITenantFeatureFlagProvider provider)
        {
            _provider = provider;
        }

        /// <summary>
        /// Checks if a specific feature is enabled for a given tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier. Example: <c>tenant1</c></param>
        /// <param name="featureName">The feature name. Example: <c>NewDashboard</c></param>
        /// <returns>True if enabled, false otherwise.</returns>
        /// <response code="200">Returns the feature status for the tenant.</response>
        /// <example>
        /// GET /api/features/tenant1/is-enabled/NewDashboard
        /// Response:
        /// {
        ///   "tenantId": "tenant1",
        ///   "featureName": "NewDashboard",
        ///   "enabled": true
        /// }
        /// </example>
        [HttpGet("{tenantId}/is-enabled/{featureName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> IsEnabled(string tenantId, string featureName)
        {
            var enabled = await _provider.IsEnabledAsync(tenantId, featureName);
            return Ok(new { tenantId, featureName, enabled });
        }

        /// <summary>
        /// Gets all enabled features for a given tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier. Example: <c>tenant1</c></param>
        /// <returns>List of enabled feature names.</returns>
        /// <response code="200">Returns a list of enabled features.</response>
        /// <example>
        /// GET /api/features/tenant1/list
        /// Response:
        /// [
        ///   "NewDashboard",
        ///   "BetaFeature"
        /// ]
        /// </example>
        [HttpGet("{tenantId}/list")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ListEnabled(string tenantId)
        {
            var features = await _provider.GetEnabledFeaturesAsync(tenantId);
            return Ok(features);
        }
    }
}