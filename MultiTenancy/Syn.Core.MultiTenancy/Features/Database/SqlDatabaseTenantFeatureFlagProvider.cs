using Microsoft.Data.SqlClient;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Syn.Core.MultiTenancy.Features.Database
{
    /// <summary>
    /// Tenant-aware feature flag provider backed by raw SQL using SqlConnection.
    /// </summary>
    public sealed class SqlDatabaseTenantFeatureFlagProvider : ITenantFeatureFlagProvider
    {
        private readonly string _connectionString;

        public SqlDatabaseTenantFeatureFlagProvider(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<IEnumerable<string>> GetEnabledFeaturesAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            var result = new List<string>();

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(
                "SELECT FeatureName FROM TenantFeatureFlags WHERE TenantId = @TenantId AND IsEnabled = 1",
                connection);

            command.Parameters.AddWithValue("@TenantId", tenantId);

            await connection.OpenAsync(cancellationToken);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(reader.GetString(0));
            }

            return result;
        }

        public async Task<bool> IsEnabledAsync(string tenantId, string featureName, CancellationToken cancellationToken = default)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(
                "SELECT COUNT(*) FROM TenantFeatureFlags WHERE TenantId = @TenantId AND FeatureName = @FeatureName AND IsEnabled = 1",
                connection);

            command.Parameters.AddWithValue("@TenantId", tenantId);
            command.Parameters.AddWithValue("@FeatureName", featureName);

            await connection.OpenAsync(cancellationToken);

            var count = (int)await command.ExecuteScalarAsync(cancellationToken);
            return count > 0;
        }
    }


}
