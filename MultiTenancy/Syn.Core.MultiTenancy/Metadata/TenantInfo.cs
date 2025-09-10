namespace Syn.Core.MultiTenancy.Metadata
{
    /// <summary>
    /// Immutable tenant metadata used by resolvers and context factories.
    /// </summary>
    public sealed class TenantInfo
    {
        public string TenantId { get; }
        public string? DisplayName { get; }
        public string ConnectionString { get; }
        public string? SchemaName { get; }
        public bool IsActive { get; }
        public DateTimeOffset CreatedAtUtc { get; }

        /// <summary>
        /// Optional metadata dictionary for tenant-specific configuration.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        public TenantInfo(
            string tenantId,
            string connectionString,
            string? schemaName = null,
            string? displayName = null,
            bool isActive = true,
            DateTimeOffset? createdAtUtc = null)
        {
            TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
            ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            SchemaName = schemaName;
            DisplayName = displayName;
            IsActive = isActive;
            CreatedAtUtc = createdAtUtc ?? DateTimeOffset.UtcNow;
        }
    }
}