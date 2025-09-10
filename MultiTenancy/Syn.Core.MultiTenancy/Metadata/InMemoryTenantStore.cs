namespace Syn.Core.MultiTenancy.Metadata
{
    /// <summary>
    /// In-memory implementation of <see cref="ITenantStore"/> for demonstration and testing purposes.
    /// Supports filtering by active status via the includeInactive parameter.
    /// Designed to work with immutable <see cref="TenantInfo"/> objects.
    /// </summary>
    public class InMemoryTenantStore : ITenantStore
    {
        private readonly List<TenantInfo> _tenants;

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryTenantStore"/> class.
        /// </summary>
        /// <param name="tenants">Initial list of tenants to seed the store.</param>
        public InMemoryTenantStore(IEnumerable<TenantInfo> tenants)
        {
            _tenants = tenants?.ToList() ?? throw new ArgumentNullException(nameof(tenants));
        }
        /// <inheritdoc />
        public TenantInfo? Get(string tenantId, bool includeInactive = false)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID cannot be null or empty.", nameof(tenantId));

            var tenant = _tenants.FirstOrDefault(t =>
                t.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase) &&
                (includeInactive || t.IsActive));

            return tenant;
        }
        /// <inheritdoc />
        public IReadOnlyList<TenantInfo> GetAll(bool includeInactive = false)
        {
            var tenants = includeInactive
                ? _tenants
                : _tenants.Where(t => t.IsActive).ToList();

            return tenants;
        }

        /// <inheritdoc />
        public Task<TenantInfo?> GetAsync(string tenantId, bool includeInactive = false, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID cannot be null or empty.", nameof(tenantId));

            var tenant = _tenants.FirstOrDefault(t =>
                t.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase) &&
                (includeInactive || t.IsActive));

            return Task.FromResult(tenant);
        }

        /// <inheritdoc />
        public void AddOrUpdate(TenantInfo tenant)
        {
            if (tenant == null)
                throw new ArgumentNullException(nameof(tenant));

            var index = _tenants.FindIndex(t =>
                t.TenantId.Equals(tenant.TenantId, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                var existing = _tenants[index];

                // Create a new immutable instance preserving CreatedAtUtc
                var updated = new TenantInfo(
                    tenant.TenantId,
                    tenant.ConnectionString,
                    tenant.SchemaName,
                    tenant.DisplayName,
                    tenant.IsActive,
                    existing.CreatedAtUtc
                );

                _tenants[index] = updated;
            }
            else
            {
                _tenants.Add(tenant);
            }

        }

        /// <inheritdoc />
        public bool Delete(string tenantId)
        {
            var tenant = _tenants.FirstOrDefault(t =>
                t.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase));

            if (tenant != null)
            {
                _tenants.Remove(tenant);
                return true;
            }

            return false;
        }


        /// <inheritdoc />
        public Task<IReadOnlyList<TenantInfo>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default)
        {
            var tenants = includeInactive
                ? _tenants
                : _tenants.Where(t => t.IsActive).ToList();

            return Task.FromResult((IReadOnlyList<TenantInfo>)tenants);
        }

        /// <inheritdoc />
        public Task AddOrUpdateAsync(TenantInfo tenant, CancellationToken ct = default)
        {
            if (tenant == null)
                throw new ArgumentNullException(nameof(tenant));

            var index = _tenants.FindIndex(t =>
                t.TenantId.Equals(tenant.TenantId, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                var existing = _tenants[index];

                // Create a new immutable instance preserving CreatedAtUtc
                var updated = new TenantInfo(
                    tenant.TenantId,
                    tenant.ConnectionString,
                    tenant.SchemaName,
                    tenant.DisplayName,
                    tenant.IsActive,
                    existing.CreatedAtUtc
                );

                _tenants[index] = updated;
            }
            else
            {
                _tenants.Add(tenant);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<bool> DeleteAsync(string tenantId, CancellationToken ct = default)
        {
            var tenant = _tenants.FirstOrDefault(t =>
                t.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase));

            if (tenant != null)
            {
                _tenants.Remove(tenant);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }
}