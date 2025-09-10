using Microsoft.EntityFrameworkCore;

using Syn.Core.SqlSchemaGenerator.Builders;
using Syn.Core.SqlSchemaGenerator.Models;

using System.Linq.Expressions;
using System.Reflection;

namespace Syn.Core.SqlSchemaGenerator.Extensions;

public static class EntityDefinitionsToModelxtension
{
    /// <summary>
    /// Scans a single assembly for entity types, optionally filtered by one or more base classes or interfaces,
    /// then applies their definitions to the EF Core ModelBuilder.
    /// </summary>
    /// <param name="builder">The EF Core ModelBuilder instance.</param>
    /// <param name="assembly">The assembly to scan for entity types.</param>
    /// <param name="filterTypes">Optional filter types (interfaces or base classes).</param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void ApplyEntityDefinitionsToModel(
        this ModelBuilder builder,
        Assembly assembly,
        params Type[] filterTypes)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));

        var entityTypes = assembly.FilterTypesFromAssembly(filterTypes);
        ApplyEntityDefinitionsToModel(builder, entityTypes);
    }

    /// <summary>
    /// Scans a single assembly for entity types assignable to the specified generic type,
    /// then applies their definitions to the EF Core ModelBuilder.
    /// </summary>
    /// <typeparam name="T">Base class or interface to filter entity types.</typeparam>
    /// <param name="builder">The EF Core ModelBuilder instance.</param>
    /// <param name="assembly">The assembly to scan for entity types.</param>
    public static void ApplyEntityDefinitionsToModel<T>(
        this ModelBuilder builder,
        Assembly assembly)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));

        var entityTypes = assembly.FilterTypesFromAssembly(typeof(T));
        ApplyEntityDefinitionsToModel(builder, entityTypes);
    }

    /// <summary>
    /// Scans a single assembly for entity types assignable to both specified generic types,
    /// then applies their definitions to the EF Core ModelBuilder.
    /// </summary>
    /// <typeparam name="T1">First base class or interface to filter entity types.</typeparam>
    /// <typeparam name="T2">Second base class or interface to filter entity types.</typeparam>
    /// <param name="builder">The EF Core ModelBuilder instance.</param>
    /// <param name="assembly">The assembly to scan for entity types.</param>
    public static void ApplyEntityDefinitionsToModel<T1, T2>(
        this ModelBuilder builder,
        Assembly assembly)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));

        var entityTypes = assembly.FilterTypesFromAssembly(typeof(T1), typeof(T2));
        ApplyEntityDefinitionsToModel(builder, entityTypes);
    }


    /// <summary>
    /// Scans multiple assemblies for entity types, optionally filtered by one or more base classes or interfaces,
    /// then applies their definitions to the EF Core ModelBuilder.
    /// </summary>
    /// <param name="builder">The EF Core ModelBuilder instance.</param>
    /// <param name="assemblies">The assemblies to scan for entity types.</param>
    /// <param name="filterTypes">Optional filter types (interfaces or base classes).</param>
    public static void ApplyEntityDefinitionsToModel(
        this ModelBuilder builder,
        IEnumerable<Assembly> assemblies,
        params Type[] filterTypes)
    {
        if (assemblies == null) throw new ArgumentNullException(nameof(assemblies));

        var entityTypes = assemblies.FilterTypesFromAssemblies(filterTypes);
        ApplyEntityDefinitionsToModel(builder, entityTypes);
    }

    /// <summary>
    /// Scans multiple assemblies for entity types assignable to the specified generic type,
    /// then applies their definitions to the EF Core ModelBuilder.
    /// </summary>
    /// <typeparam name="T">Base class or interface to filter entity types.</typeparam>
    /// <param name="builder">The EF Core ModelBuilder instance.</param>
    /// <param name="assemblies">The assemblies to scan for entity types.</param>
    /// <param name="schemaName">Shared tenant schema</param>
    /// <param name="tenantIdPropertyName"></param>
    /// <param name="tenantIdValue"></param>
    public static void ApplyEntityDefinitionsToModel<T>(
        this ModelBuilder builder,
        IEnumerable<Assembly> assemblies)
    {
        if (assemblies == null) throw new ArgumentNullException(nameof(assemblies));

        var entityTypes = assemblies.FilterTypesFromAssemblies(typeof(T));
        ApplyEntityDefinitionsToModel(builder, entityTypes);
    }

    /// <summary>
    /// Scans multiple assemblies for entity types assignable to both specified generic types,
    /// then applies their definitions to the EF Core ModelBuilder.
    /// </summary>
    /// <typeparam name="T1">First base class or interface to filter entity types.</typeparam>
    /// <typeparam name="T2">Second base class or interface to filter entity types.</typeparam>
    /// <param name="builder">The EF Core ModelBuilder instance.</param>
    /// <param name="assemblies">The assemblies to scan for entity types.</param>
    public static void ApplyEntityDefinitionsToModel<T1, T2>(
        this ModelBuilder builder,
        IEnumerable<Assembly> assemblies)
    {
        if (assemblies == null) throw new ArgumentNullException(nameof(assemblies));

        var entityTypes = assemblies.FilterTypesFromAssemblies(typeof(T1), typeof(T2));
        ApplyEntityDefinitionsToModel(builder, entityTypes);
    }


    /// <summary>
    /// Builds entity definitions from the provided CLR types (including relationships)
    /// and applies them to the EF Core ModelBuilder.
    /// Configures:
    /// - Columns (type, nullability, precision, defaults, computed columns, collation, comments)
    /// - Primary keys, unique constraints, indexes, check constraints
    /// - Relationships (One-to-One, One-to-Many, Many-to-One, Many-to-Many)
    /// - Foreign keys automatically from column metadata
    /// </summary>
    /// <param name="builder">The EF Core ModelBuilder instance.</param>
    /// <param name="entityTypes">The CLR types representing entities to configure.</param>
    public static void ApplyEntityDefinitionsToModel(
        this ModelBuilder builder,
        IEnumerable<Type> entityTypes)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));
        if (entityTypes == null) throw new ArgumentNullException(nameof(entityTypes));
        var _entityDefinitionBuilder = new EntityDefinitionBuilder();
        var entities = _entityDefinitionBuilder
            .BuildAllWithRelationships(entityTypes)
            .ToList();

        foreach (var entity in entities)
        {
            var entityBuilder = builder.Entity(entity.ClrType);

            // ===== Columns =====
            foreach (var column in entity.Columns)
            {
                var propertyBuilder = entityBuilder.Property(column.PropertyType, column.Name);

                if (!column.IsNullable)
                    propertyBuilder.IsRequired();

                if (column.Precision.HasValue && column.Scale.HasValue)
                    propertyBuilder.HasPrecision(column.Precision.Value, column.Scale.Value);
                else if (column.Precision.HasValue)
                    propertyBuilder.HasPrecision(column.Precision.Value);

                if (column.DefaultValue != null)
                    propertyBuilder.HasDefaultValue(column.DefaultValue);

                if (!string.IsNullOrWhiteSpace(column.ComputedExpression))
                    propertyBuilder.HasComputedColumnSql(column.ComputedExpression, column.IsPersisted);

                if (!string.IsNullOrWhiteSpace(column.Collation))
                    propertyBuilder.UseCollation(column.Collation);

                if (!string.IsNullOrWhiteSpace(column.Comment))
                    propertyBuilder.HasComment(column.Comment);
                else if (!string.IsNullOrWhiteSpace(column.Description))
                    propertyBuilder.HasComment(column.Description);

                if (column.IsPrimaryKey)
                    entityBuilder.HasKey(column.Name);

                if (column.IsUnique)
                {
                    var idx = entityBuilder.HasIndex(column.Name).IsUnique();
                    if (!string.IsNullOrWhiteSpace(column.UniqueConstraintName))
                        idx.HasDatabaseName(column.UniqueConstraintName);
                }

                foreach (var check in column.CheckConstraints)
                {
                    entityBuilder.ToTable(tb =>
                    {
                        tb.HasCheckConstraint(check.Name, check.Expression);
                    });
                }

                foreach (var index in column.Indexes)
                {
                    var idxBuilder = entityBuilder.HasIndex(index.Columns.ToArray());
                    if (index.IsUnique)
                        idxBuilder.IsUnique();
                    if (!string.IsNullOrWhiteSpace(index.Name))
                        idxBuilder.HasDatabaseName(index.Name);
                }

                if (column.IsForeignKey && !string.IsNullOrWhiteSpace(column.ForeignKeyTarget))
                {
                    var targetEntity = entities.FirstOrDefault(e =>
                        string.Equals(e.Name, column.ForeignKeyTarget, StringComparison.OrdinalIgnoreCase));

                    if (targetEntity != null)
                    {
                        entityBuilder
                            .HasOne(targetEntity.ClrType)
                            .WithMany()
                            .HasForeignKey(column.Name);
                    }
                }
            }

            // ===== Relationships =====
            foreach (var rel in entity.Relationships)
            {
                var sourceEntityDef = entities.FirstOrDefault(e =>
                    string.Equals(e.Name, rel.SourceEntity, StringComparison.OrdinalIgnoreCase));
                var targetEntityDef = entities.FirstOrDefault(e =>
                    string.Equals(e.Name, rel.TargetEntity, StringComparison.OrdinalIgnoreCase));

                if (sourceEntityDef == null || targetEntityDef == null)
                    continue;

                var sourceBuilder = builder.Entity(sourceEntityDef.ClrType);
                var deleteBehavior = MapDeleteBehavior(rel.OnDelete);

                switch (rel.Type)
                {
                    case RelationshipType.OneToMany:
                        sourceBuilder
                            .HasMany(targetEntityDef.ClrType, rel.SourceProperty)
                            .WithOne(rel.TargetProperty)
                            .HasForeignKey(rel.SourceToTargetColumn)
                            .IsRequired(rel.IsRequired)
                            .OnDelete(deleteBehavior);
                        break;

                    case RelationshipType.ManyToOne:
                        sourceBuilder
                            .HasOne(targetEntityDef.ClrType, rel.SourceProperty)
                            .WithMany(rel.TargetProperty)
                            .HasForeignKey(rel.SourceToTargetColumn)
                            .IsRequired(rel.IsRequired)
                            .OnDelete(deleteBehavior);
                        break;

                    case RelationshipType.OneToOne:
                        sourceBuilder
                            .HasOne(targetEntityDef.ClrType, rel.SourceProperty)
                            .WithOne(rel.TargetProperty)
                            .HasForeignKey(sourceEntityDef.ClrType, rel.SourceToTargetColumn)
                            .IsRequired(rel.IsRequired)
                            .OnDelete(deleteBehavior);
                        break;

                    case RelationshipType.ManyToMany:
                        var manyToMany = sourceBuilder
                            .HasMany(targetEntityDef.ClrType, rel.SourceProperty)
                            .WithMany(rel.TargetProperty);

                        if (!string.IsNullOrWhiteSpace(rel.JoinEntityName))
                        {
                            manyToMany.UsingEntity(rel.JoinEntityName);
                        }
                        break;
                }
            }
        }
    }
    /// <summary>
    /// Maps a System.Data ReferentialAction to EF Core DeleteBehavior.
    /// </summary>
    private static DeleteBehavior MapDeleteBehavior(ReferentialAction action)
    {
        return action switch
        {
            ReferentialAction.Cascade => DeleteBehavior.Cascade,
            ReferentialAction.SetNull => DeleteBehavior.SetNull,
            ReferentialAction.Restrict => DeleteBehavior.Restrict,
            _ => DeleteBehavior.NoAction
        };
    }
}
