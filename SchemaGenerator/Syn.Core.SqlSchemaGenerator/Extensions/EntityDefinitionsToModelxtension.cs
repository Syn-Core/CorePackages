using Microsoft.EntityFrameworkCore;
using Syn.Core.SqlSchemaGenerator.Builders;
using Syn.Core.SqlSchemaGenerator.Models;
using System.Reflection;
using Microsoft.EntityFrameworkCore.SqlServer;

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

        var defBuilder = new EntityDefinitionBuilder();
        var entities = defBuilder.BuildAllWithRelationships(entityTypes).ToList();

        var addedColumnsPerTable = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var processedNavigations = new HashSet<(string, string, string, string)>();
        var processedManyToManyPairs = new HashSet<(string, string)>(new TupleIgnoreCaseComparer());

        foreach (var entity in entities)
        {
            // 🆕 دعم تسجيل الـ Shadow Join Table
            if (entity.ClrType == null && entity.IsShadowEntity)
            {
                Console.WriteLine($"[TRACE:ShadowEntity] Registering shadow join table '{entity.Name}'");

                var shadowBuilder = builder.SharedTypeEntity<Dictionary<string, object>>(entity.Name);

                // 🆕 تطبيق الـ Schema لو موجود
                if (!string.IsNullOrWhiteSpace(entity.Schema))
                    shadowBuilder.ToTable(entity.Name, entity.Schema);

                // PK
                if (entity.PrimaryKey != null && entity.PrimaryKey.Columns.Any())
                {
                    var pkBuilder = shadowBuilder.HasKey(entity.PrimaryKey.Columns.ToArray());

                    if (!string.IsNullOrWhiteSpace(entity.PrimaryKey.Name))
                        pkBuilder.HasName(entity.PrimaryKey.Name);

                    if (builder.Model.GetProductVersion().Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
                        pkBuilder.IsClustered(entity.PrimaryKey.IsClustered);
                }

                // FKs
                foreach (var fk in entity.ForeignKeys)
                {
                    var targetEntity = entities.FirstOrDefault(e =>
                        e.Name.Equals(fk.ReferencedTable, StringComparison.OrdinalIgnoreCase));
                    if (targetEntity?.ClrType != null)
                    {
                        shadowBuilder.HasOne(targetEntity.ClrType)
                                     .WithMany()
                                     .HasForeignKey(fk.Column)
                                     .OnDelete(DeleteBehavior.Cascade);
                    }
                }

                // الأعمدة
                foreach (var column in entity.Columns)
                {
                    shadowBuilder.Property(column.PropertyType ?? typeof(object), column.Name)
                                 .IsRequired(!column.IsNullable);
                }

                continue;
            }

            if (entity.ClrType == null && !entity.IsShadowEntity)
            {
                Console.WriteLine($"[TRACE:SkipEntity] '{entity.Name}' has no CLR type and is not shadow.");
                continue;
            }

            var entityBuilder = builder.Entity(entity.ClrType);

            // 🆕 تطبيق الـ Schema لو موجود
            if (!string.IsNullOrWhiteSpace(entity.Schema))
                entityBuilder.ToTable(entity.Name, entity.Schema);

            // PK من PrimaryKeyDefinition
            if (entity.PrimaryKey != null && entity.PrimaryKey.Columns.Any())
            {
                var pkBuilder = entityBuilder.HasKey(entity.PrimaryKey.Columns.ToArray());

                if (!string.IsNullOrWhiteSpace(entity.PrimaryKey.Name))
                    pkBuilder.HasName(entity.PrimaryKey.Name);

                if (builder.Model.GetProductVersion().Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
                    pkBuilder.IsClustered(entity.PrimaryKey.IsClustered);
            }

            if (!addedColumnsPerTable.ContainsKey(entity.Name))
                addedColumnsPerTable[entity.Name] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // الأعمدة العادية
            foreach (var column in entity.Columns)
            {
                if (addedColumnsPerTable[entity.Name].Contains(column.Name))
                    continue;

                var propertyBuilder = entityBuilder.Property(column.PropertyType ?? column.PropertyType, column.Name);

                if (!column.IsNullable)
                    propertyBuilder.IsRequired();

                addedColumnsPerTable[entity.Name].Add(column.Name);
            }

            // العلاقات
            foreach (var rel in entity.Relationships)
            {
                if (rel.Type == RelationshipType.ManyToMany)
                {
                    var orderedPair = string.Compare(rel.SourceEntity, rel.TargetEntity, StringComparison.OrdinalIgnoreCase) < 0
                        ? (rel.SourceEntity, rel.TargetEntity)
                        : (rel.TargetEntity, rel.SourceEntity);

                    if (processedManyToManyPairs.Contains(orderedPair))
                        continue;

                    processedManyToManyPairs.Add(orderedPair);
                }

                var navKey = (rel.SourceEntity, rel.SourceProperty ?? "", rel.TargetEntity, rel.TargetProperty ?? "");
                if (processedNavigations.Contains(navKey))
                    continue;

                processedNavigations.Add(navKey);

                var sourceEntityDef = entities.FirstOrDefault(e => e.Name.Equals(rel.SourceEntity, StringComparison.OrdinalIgnoreCase));
                var targetEntityDef = entities.FirstOrDefault(e => e.Name.Equals(rel.TargetEntity, StringComparison.OrdinalIgnoreCase));

                if (sourceEntityDef == null || targetEntityDef == null)
                    continue;

                ConfigureRelationship(builder, sourceEntityDef, targetEntityDef, rel.Type,
                    rel.SourceProperty, rel.TargetProperty,
                    rel.SourceToTargetColumn, rel.IsRequired, rel.OnDelete, rel.JoinEntityName);
            }
        }
    }


    private static void ConfigureRelationship(
        ModelBuilder builder,
        EntityDefinition sourceEntity,
        EntityDefinition targetEntity,
        RelationshipType type,
        string sourceProperty,
        string targetProperty,
        string sourceToTargetColumn,
        bool isRequired,
        ReferentialAction onDelete,
        string joinEntityName = null)
    {
        var deleteBehavior = MapDeleteBehavior(onDelete);
        Console.WriteLine($"[TRACE:Configure] {type} {sourceEntity.Name}.{sourceProperty} -> {targetEntity.Name}.{targetProperty ?? "(null)"} FK: {sourceToTargetColumn}");

        var sourceBuilder = builder.Entity(sourceEntity.ClrType);

        switch (type)
        {
            case RelationshipType.OneToMany:
                sourceBuilder
                    .HasMany(targetEntity.ClrType, sourceProperty)
                    .WithOne(targetProperty)
                    .HasForeignKey(sourceToTargetColumn)
                    .IsRequired(isRequired)
                    .OnDelete(deleteBehavior);
                break;

            case RelationshipType.ManyToOne:
                sourceBuilder
                    .HasOne(targetEntity.ClrType, sourceProperty)
                    .WithMany(targetProperty)
                    .HasForeignKey(sourceToTargetColumn)
                    .IsRequired(isRequired)
                    .OnDelete(deleteBehavior);
                break;

            case RelationshipType.OneToOne:
                sourceBuilder
                    .HasOne(targetEntity.ClrType, sourceProperty)
                    .WithOne(targetProperty)
                    .HasForeignKey(sourceEntity.ClrType, sourceToTargetColumn)
                    .IsRequired(isRequired)
                    .OnDelete(deleteBehavior);
                break;

            case RelationshipType.ManyToMany:
                var manyToMany = sourceBuilder
                    .HasMany(targetEntity.ClrType, sourceProperty)
                    .WithMany(targetProperty);

                if (!string.IsNullOrWhiteSpace(joinEntityName))
                {
                    Console.WriteLine($"[TRACE:Configure] Using join table: {joinEntityName}");

                    // Shadow join table
                    manyToMany.UsingEntity(
                        joinEntityName,
                        l => l.HasOne(targetEntity.ClrType)
                              .WithMany()
                              .HasForeignKey($"{targetEntity.Name}Id")
                              .OnDelete(deleteBehavior),
                        r => r.HasOne(sourceEntity.ClrType)
                              .WithMany()
                              .HasForeignKey($"{sourceEntity.Name}Id")
                              .OnDelete(deleteBehavior)
                    );
                }
                break;
        }
    }


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
