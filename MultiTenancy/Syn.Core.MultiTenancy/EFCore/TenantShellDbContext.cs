using Microsoft.EntityFrameworkCore;

namespace Syn.Core.MultiTenancy.EFCore
{
    /// <summary>
    /// A shell DbContext that applies a default schema and includes a set of entity types dynamically.
    /// </summary>
    public sealed class TenantShellDbContext : DbContext
    {
        private readonly string? _schema;
        private readonly IReadOnlyCollection<Type> _entityTypes;

        public TenantShellDbContext(DbContextOptions<TenantShellDbContext> options, string? schema, IEnumerable<Type> entityTypes)
            : base(options)
        {
            _schema = schema;
            _entityTypes = entityTypes.ToList();
        }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (!string.IsNullOrWhiteSpace(_schema))
                modelBuilder.HasDefaultSchema(_schema);

            // Register each entity type in the model.
            foreach (var type in _entityTypes)
            {
                modelBuilder.Entity(type);
            }

            base.OnModelCreating(modelBuilder);
        }
    }
}
