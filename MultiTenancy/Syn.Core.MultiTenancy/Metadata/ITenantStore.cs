namespace Syn.Core.MultiTenancy.Metadata
{
    /// <summary>
    /// Abstraction for retrieving and managing tenant metadata from any backing store.
    /// </summary>
    public interface ITenantStore
    {
        /// <summary>
        /// Retrieves a tenant by its identifier.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="includeInactive">
        /// If <c>true</c>, inactive tenants will also be returned.
        /// If <c>false</c>, only active tenants will be returned.
        /// </param>
        TenantInfo? Get(string tenantId, bool includeInactive = false);

        /// <summary>
        /// Retrieves all tenants.
        /// </summary>
        /// <param name="includeInactive">
        /// If <c>true</c>, returns all tenants including inactive ones.
        /// If <c>false</c>, returns only active tenants.
        /// </param>
        IReadOnlyList<TenantInfo> GetAll(bool includeInactive = false);

        /// <summary>
        /// Adds or updates a tenant in the store.
        /// </summary>
        /// <param name="tenant">The tenant metadata.</param>
        public void AddOrUpdate(TenantInfo tenant);

        /// <summary>
        /// Deletes a tenant by its identifier.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        public bool Delete(string tenantId);

        /// <summary>
        /// Retrieves a tenant by its identifier.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="includeInactive">
        /// If <c>true</c>, inactive tenants will also be returned.
        /// If <c>false</c>, only active tenants will be returned.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        Task<TenantInfo?> GetAsync(string tenantId, bool includeInactive = false, CancellationToken ct = default);

        /// <summary>
        /// Retrieves all tenants.
        /// </summary>
        /// <param name="includeInactive">
        /// If <c>true</c>, returns all tenants including inactive ones.
        /// If <c>false</c>, returns only active tenants.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        Task<IReadOnlyList<TenantInfo>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default);

        /// <summary>
        /// Adds or updates a tenant in the store.
        /// </summary>
        /// <param name="tenant">The tenant metadata.</param>
        /// <param name="ct">Cancellation token.</param>
        Task AddOrUpdateAsync(TenantInfo tenant, CancellationToken ct = default);

        /// <summary>
        /// Deletes a tenant by its identifier.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="ct">Cancellation token.</param>
        Task<bool> DeleteAsync(string tenantId, CancellationToken ct = default);
    }
}