using Microsoft.AspNetCore.Mvc;

using Syn.Core.MultiTenancy.Metadata;

namespace Sample.Web.Controllers
{
    /// <summary>
    /// Provides endpoints to list all known tenants in the system.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [ApiExplorerSettings(GroupName = "Tenants")]
    public class TenantsController : ControllerBase
    {
        private readonly IEnumerable<TenantInfo> _tenants;

        public TenantsController(IEnumerable<TenantInfo> tenants)
        {
            _tenants = tenants;
        }

        /// <summary>
        /// Gets all registered tenants (read-only for demo purposes).
        /// </summary>
        /// <returns>List of TenantInfo objects.</returns>
        /// <response code="200">Returns the list of known tenants.</response>
        /// <example>
        /// GET /api/tenants
        /// Response:
        /// [
        ///   { "tenantId": "tenant1", "displayName": null, "connectionString": "Server=...;Database=Tenant1;...", "schemaName": null, "isActive": true, "createdAtUtc": "2025-01-01T00:00:00Z", "metadata": {} },
        ///   { "tenantId": "tenant2", "displayName": null, "connectionString": "Server=...;Database=Tenant2;...", "schemaName": null, "isActive": true, "createdAtUtc": "2025-01-01T00:00:00Z", "metadata": {} }
        /// ]
        /// </example>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetAll() => Ok(_tenants);
    }
}