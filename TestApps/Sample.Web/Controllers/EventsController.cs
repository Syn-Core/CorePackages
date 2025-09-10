using Microsoft.AspNetCore.Mvc;

using Sample.Web.Models;

using Syn.Core.MultiTenancy.Events;
using Syn.Core.MultiTenancy.Metadata;

namespace Sample.Web.Controllers
{
    /// <summary>
    /// Provides endpoints to publish tenant lifecycle events for testing event handlers.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [ApiExplorerSettings(GroupName = "Events")]
    public class EventsController : ControllerBase
    {
        private readonly TenantEventPublisher _publisher;
        private readonly IEnumerable<TenantInfo> _tenants;

        public EventsController(TenantEventPublisher publisher, IEnumerable<TenantInfo> tenants)
        {
            _publisher = publisher;
            _tenants = tenants;
        }

        private TenantInfo? GetTenant(string tenantId) =>
            _tenants.FirstOrDefault(t => t.TenantId == tenantId);

        /// <summary>
        /// Publishes a "Tenant Created" event.
        /// </summary>
        /// <param name="tenantId">The tenant identifier. Example: <c>tenant1</c></param>
        /// <response code="200">Returns confirmation that the event was published.</response>
        /// <example>
        /// POST /api/events/tenant1/created
        /// Response:
        /// {
        ///   "tenantId": "tenant1",
        ///   "eventType": "Created",
        ///   "published": true
        /// }
        /// </example>
        [HttpPost("{tenantId}/created")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> PublishCreated(string tenantId)
        {
            var tenant = GetTenant(tenantId);
            if (tenant == null) return NotFound($"Tenant '{tenantId}' not found.");

            await _publisher.PublishCreatedAsync(tenant);
            return Ok(new { tenantId, eventType = "Created", published = true });
        }

        /// <summary>
        /// Publishes a "Tenant Activated" event.
        /// </summary>
        /// <param name="tenantId">The tenant identifier. Example: <c>tenant1</c></param>
        /// <response code="200">Returns confirmation.</response>
        /// <example>
        /// POST /api/events/tenant1/activated
        /// Response:
        /// { "tenantId": "tenant1", "eventType": "Activated", "published": true }
        /// </example>
        [HttpPost("{tenantId}/activated")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> PublishActivated(string tenantId)
        {
            var tenant = GetTenant(tenantId);
            if (tenant == null) return NotFound($"Tenant '{tenantId}' not found.");

            await _publisher.PublishActivatedAsync(tenant);
            return Ok(new { tenantId, eventType = "Activated", published = true });
        }

        /// <summary>
        /// Publishes a "Tenant Deactivated" event.
        /// </summary>
        /// <param name="tenantId">The tenant identifier. Example: <c>tenant1</c></param>
        /// <response code="200">Returns confirmation.</response>
        /// <example>
        /// POST /api/events/tenant1/deactivated
        /// Response:
        /// { "tenantId": "tenant1", "eventType": "Deactivated", "published": true }
        /// </example>
        [HttpPost("{tenantId}/deactivated")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> PublishDeactivated(string tenantId)
        {
            var tenant = GetTenant(tenantId);
            if (tenant == null) return NotFound($"Tenant '{tenantId}' not found.");

            await _publisher.PublishDeactivatedAsync(tenant);
            return Ok(new { tenantId, eventType = "Deactivated", published = true });
        }

        /// <summary>
        /// Publishes a "Tenant Deleted" event.
        /// </summary>
        /// <param name="tenantId">The tenant identifier. Example: <c>tenant1</c></param>
        /// <response code="200">Returns confirmation.</response>
        /// <example>
        /// POST /api/events/tenant1/deleted
        /// Response:
        /// { "tenantId": "tenant1", "eventType": "Deleted", "published": true }
        /// </example>
        [HttpPost("{tenantId}/deleted")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> PublishDeleted(string tenantId)
        {
            var tenant = GetTenant(tenantId);
            if (tenant == null) return NotFound($"Tenant '{tenantId}' not found.");

            await _publisher.PublishDeletedAsync(tenant);
            return Ok(new { tenantId, eventType = "Deleted", published = true });
        }

        /// <summary>
        /// Publishes a "Tenant Metadata Changed" event after merging the provided metadata into the tenant's metadata (demo only).
        /// </summary>
        /// <param name="tenantId">The tenant identifier. Example: <c>tenant1</c></param>
        /// <param name="request">The metadata changes to merge.</param>
        /// <response code="200">Returns confirmation.</response>
        /// <example>
        /// POST /api/events/tenant1/metadata-changed
        /// Body:
        /// {
        ///   "metadata": { "plan": "pro", "region": "eu" }
        /// }
        /// Response:
        /// {
        ///   "tenantId": "tenant1",
        ///   "eventType": "MetadataChanged",
        ///   "published": true,
        ///   "metadata": { "plan": "pro", "region": "eu" }
        /// }
        /// </example>
        [HttpPost("{tenantId}/metadata-changed")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> PublishMetadataChanged(string tenantId, [FromBody] MetadataChangeRequest request)
        {
            var tenant = GetTenant(tenantId);
            if (tenant == null) return NotFound($"Tenant '{tenantId}' not found.");

            // Demo-only merge of in-memory metadata
            foreach (var kvp in request.Metadata)
                tenant.Metadata[kvp.Key] = kvp.Value;

            await _publisher.PublishMetadataChangedAsync(tenant);
            return Ok(new { tenantId, eventType = "MetadataChanged", published = true, metadata = tenant.Metadata });
        }
    }
}