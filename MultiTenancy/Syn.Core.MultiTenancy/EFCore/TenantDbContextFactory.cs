using Microsoft.EntityFrameworkCore;

using Syn.Core.MultiTenancy.Resolvers;

namespace Syn.Core.MultiTenancy.EFCore
{
    /// <summary>
    /// Default DbContext factory that configures per-tenant connection string and schema.
    /// </summary>
    public sealed class TenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly ITenantConnectionStringResolver _connectionResolver;
        private readonly ITenantSchemaResolver _schemaResolver;
        private readonly IServiceProvider _rootProvider;

        /// <summary>
        /// Initializes a new instance of <see cref="TenantDbContextFactory"/>.
        /// </summary>
        public TenantDbContextFactory(
            ITenantConnectionStringResolver connectionResolver,
            ITenantSchemaResolver schemaResolver,
            IServiceProvider rootProvider)
        {
            _connectionResolver = connectionResolver;
            _schemaResolver = schemaResolver;
            _rootProvider = rootProvider;
        }

        /// <inheritdoc />
        public DbContext Create(string tenantId, IEnumerable<Type> entityTypes)
        {
            var connectionString = _connectionResolver.Resolve(tenantId);
            var schema = _schemaResolver.Resolve(tenantId);

            // Build a dynamic DbContext type that includes the provided entity types.
            // For simplicity, we use a generic shell context that maps entity sets at runtime.
            var optionsBuilder = new DbContextOptionsBuilder<TenantShellDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new TenantShellDbContext(optionsBuilder.Options, schema, entityTypes);
        }
    }
}
