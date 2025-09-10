using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using Sample.Web.Data;
using Sample.Web.Models;

using Syn.Core.MultiTenancy.Features.Database;
using Syn.Core.MultiTenancy.Metadata;

namespace Sample.Web.Controllers
{
    /// <summary>
    /// Provides administrative endpoints to add, update, or delete feature flags for tenants.
    /// Works with both EF Core and SQL providers (selected via configuration).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [ApiExplorerSettings(GroupName = "Feature Admin")]
    public class FeatureAdminController : ControllerBase
    {
        private readonly IEnumerable<TenantInfo> _tenants;
        private readonly IServiceProvider _sp;
        private readonly IConfiguration _config;

        public FeatureAdminController(IEnumerable<TenantInfo> tenants, IServiceProvider sp, IConfiguration config)
        {
            _tenants = tenants;
            _sp = sp;
            _config = config;
        }

        private bool UseEfProvider()
        {
            var providerType = _config["FeatureFlags:ProviderType"] ?? "EfCore";
            return providerType.Equals("EfCore", StringComparison.OrdinalIgnoreCase);
        }

        private TenantInfo? FindTenant(string tenantId) =>
            _tenants.FirstOrDefault(t => t.TenantId == tenantId);

        /// <summary>
        /// Upserts a single feature flag for the specified tenant (accepts JSON body).
        /// </summary>
        /// <param name="tenantId">The tenant identifier. Example: <c>tenant1</c></param>
        /// <param name="request">The feature upsert payload.</param>
        /// <returns>Status of the operation.</returns>
        /// <response code="200">Returns confirmation of the upsert operation.</response>
        /// <example>
        /// POST /api/featureadmin/tenant1/upsert
        /// Body:
        /// {
        ///   "featureName": "NewDashboard",
        ///   "isEnabled": true
        /// }
        /// Response:
        /// {
        ///   "tenantId": "tenant1",
        ///   "featureName": "NewDashboard",
        ///   "isEnabled": true,
        ///   "status": "Upserted"
        /// }
        /// </example>
        [HttpPost("{tenantId}/upsert")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Upsert(string tenantId, [FromBody] UpsertFeatureFlagRequest request)
        {
            var tenant = FindTenant(tenantId);
            if (tenant == null) return NotFound($"Tenant '{tenantId}' not found.");

            if (UseEfProvider())
            {
                var db = _sp.GetRequiredService<MyDbContext>();
                var set = db.Set<TenantFeatureFlag>();

                var existing = await set.FirstOrDefaultAsync(f =>
                    f.TenantId == tenantId && f.FeatureName == request.FeatureName);

                if (existing != null)
                {
                    existing.IsEnabled = request.IsEnabled;
                }
                else
                {
                    set.Add(new TenantFeatureFlag
                    {
                        TenantId = tenantId,
                        FeatureName = request.FeatureName,
                        IsEnabled = request.IsEnabled
                    });
                }

                await db.SaveChangesAsync();
            }
            else
            {
                var sql = """
                    MERGE TenantFeatureFlags AS target
                    USING (SELECT @TenantId AS TenantId, @FeatureName AS FeatureName) AS src
                    ON target.TenantId = src.TenantId AND target.FeatureName = src.FeatureName
                    WHEN MATCHED THEN UPDATE SET IsEnabled = @IsEnabled
                    WHEN NOT MATCHED THEN INSERT (Id, TenantId, FeatureName, IsEnabled) VALUES (@Id, @TenantId, @FeatureName, @IsEnabled);
                """;

                using var con = new SqlConnection(tenant.ConnectionString);
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
                cmd.Parameters.AddWithValue("@TenantId", tenantId);
                cmd.Parameters.AddWithValue("@FeatureName", request.FeatureName);
                cmd.Parameters.AddWithValue("@IsEnabled", request.IsEnabled);
                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }

            return Ok(new { tenantId, featureName = request.FeatureName, request.IsEnabled, status = "Upserted" });
        }

        /// <summary>
        /// Upserts multiple feature flags in a single request for the specified tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier. Example: <c>tenant1</c></param>
        /// <param name="request">The bulk upsert payload.</param>
        /// <returns>Status of the operation with count.</returns>
        /// <response code="200">Returns confirmation of the bulk upsert operation.</response>
        /// <example>
        /// POST /api/featureadmin/tenant1/upsert-bulk
        /// Body:
        /// {
        ///   "items": [
        ///     { "featureName": "NewDashboard", "isEnabled": true },
        ///     { "featureName": "BetaFeature",  "isEnabled": false }
        ///   ]
        /// }
        /// Response:
        /// { "tenantId": "tenant1", "count": 2, "status": "Upserted" }
        /// </example>
        [HttpPost("{tenantId}/upsert-bulk")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> UpsertBulk(string tenantId, [FromBody] BulkUpsertFeatureFlagsRequest request)
        {
            var tenant = FindTenant(tenantId);
            if (tenant == null) return NotFound($"Tenant '{tenantId}' not found.");

            var items = request.Items ?? new();

            if (UseEfProvider())
            {
                var db = _sp.GetRequiredService<MyDbContext>();
                var set = db.Set<TenantFeatureFlag>();

                foreach (var item in items)
                {
                    var existing = await set.FirstOrDefaultAsync(f =>
                        f.TenantId == tenantId && f.FeatureName == item.FeatureName);

                    if (existing != null)
                        existing.IsEnabled = item.IsEnabled;
                    else
                        set.Add(new TenantFeatureFlag { TenantId = tenantId, FeatureName = item.FeatureName, IsEnabled = item.IsEnabled });
                }

                await db.SaveChangesAsync();
            }
            else
            {
                var sql = """
                    MERGE TenantFeatureFlags AS target
                    USING (SELECT @TenantId AS TenantId, @FeatureName AS FeatureName) AS src
                    ON target.TenantId = src.TenantId AND target.FeatureName = src.FeatureName
                    WHEN MATCHED THEN UPDATE SET IsEnabled = @IsEnabled
                    WHEN NOT MATCHED THEN INSERT (TenantId, FeatureName, IsEnabled) VALUES (@TenantId, @FeatureName, @IsEnabled);
                """;

                using var con = new SqlConnection(tenant.ConnectionString);
                await con.OpenAsync();

                foreach (var item in items)
                {
                    using var cmd = new SqlCommand(sql, con);
                    cmd.Parameters.AddWithValue("@TenantId", tenantId);
                    cmd.Parameters.AddWithValue("@FeatureName", item.FeatureName);
                    cmd.Parameters.AddWithValue("@IsEnabled", item.IsEnabled);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return Ok(new { tenantId, count = items.Count, status = "Upserted" });
        }

        /// <summary>
        /// Deletes a feature flag for the specified tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier. Example: <c>tenant1</c></param>
        /// <param name="featureName">The feature name. Example: <c>NewDashboard</c></param>
        /// <returns>Status of the deletion.</returns>
        /// <response code="200">Returns confirmation of deletion.</response>
        /// <example>
        /// DELETE /api/featureadmin/tenant1/delete?featureName=NewDashboard
        /// Response:
        /// { "tenantId": "tenant1", "featureName": "NewDashboard", "status": "Deleted" }
        /// </example>
        [HttpDelete("{tenantId}/delete")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Delete(string tenantId, [FromQuery] string featureName)
        {
            var tenant = FindTenant(tenantId);
            if (tenant == null) return NotFound($"Tenant '{tenantId}' not found.");

            if (UseEfProvider())
            {
                var db = _sp.GetRequiredService<MyDbContext>();
                var set = db.Set<TenantFeatureFlag>();

                var existing = await set.FirstOrDefaultAsync(f =>
                    f.TenantId == tenantId && f.FeatureName == featureName);

                if (existing != null)
                {
                    set.Remove(existing);
                    await db.SaveChangesAsync();
                }
            }
            else
            {
                var sql = "DELETE FROM TenantFeatureFlags WHERE TenantId = @TenantId AND FeatureName = @FeatureName";
                using var con = new SqlConnection(tenant.ConnectionString);
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@TenantId", tenantId);
                cmd.Parameters.AddWithValue("@FeatureName", featureName);
                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }

            return Ok(new { tenantId, featureName, status = "Deleted" });
        }
    }
}