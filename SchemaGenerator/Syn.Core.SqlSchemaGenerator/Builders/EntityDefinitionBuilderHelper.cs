using Syn.Core.SqlSchemaGenerator.Helper;
using Syn.Core.SqlSchemaGenerator.Models;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Syn.Core.SqlSchemaGenerator.Builders;

public partial class EntityDefinitionBuilder
{
    /// <summary>
    /// Discovers all foreign key relationships for a given CLR entity type.
    /// This method merges the logic of building foreign keys from explicit FK columns
    /// (based on naming conventions or [ForeignKey] attributes) and inferring them
    /// from navigation properties.
    /// 
    /// Features:
    /// - Supports PK=FK scenarios for true one-to-one relationships.
    /// - Prevents duplicates: ensures the same FK is not added twice.
    /// - Populates all FK metadata: referenced table name, schema, and column.
    /// - Uses <see cref="GetTableInfo"/> to consistently retrieve table and schema
    ///   information from the target entity.
    /// - Works with any key type (e.g., int, Guid) without filtering out value types.
    /// 
    /// Parameters:
    /// <param name="entityType">The CLR type representing the entity.</param>
    /// <param name="entityName">The name of the current entity (used for constraint naming).</param>
    /// <returns>A complete list of <see cref="ForeignKeyDefinition"/> objects discovered.</returns>
    /// 
    /// Example:
    /// Given an entity with:
    ///   [Key]
    ///   [ForeignKey(nameof(User))]
    ///   public Guid Id { get; set; }
    ///   public User User { get; set; }
    /// This method will generate:
    ///   FK_UserProfile_Id FOREIGN KEY (Id) REFERENCES dbo.User (Id)
    /// </summary>
    internal static List<ForeignKeyDefinition> DiscoverForeignKeys(Type entityType, string entityName)
    {
        var foreignKeys = new List<ForeignKeyDefinition>();
        var props = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Helper: منع التكرار
        bool Exists(ForeignKeyDefinition fk) =>
            foreignKeys.Any(existing =>
                existing.Column.Equals(fk.Column, StringComparison.OrdinalIgnoreCase) &&
                existing.ReferencedTable.Equals(fk.ReferencedTable, StringComparison.OrdinalIgnoreCase) &&
                existing.ReferencedColumn.Equals(fk.ReferencedColumn, StringComparison.OrdinalIgnoreCase));

        // 1️⃣ Build from explicit FK columns or [ForeignKey] attributes
        foreach (var prop in props)
        {
            string columnName = GetColumnName(prop);
            string description = !string.IsNullOrWhiteSpace(prop.GetCustomAttribute<DescriptionAttribute>()?.Description) ? prop.GetCustomAttribute<DescriptionAttribute>()?.Description : null;
            var efFkAttr = prop.GetCustomAttribute<ForeignKeyAttribute>();
            if (efFkAttr != null)
            {
                var navProp = props.FirstOrDefault(p => p.Name.Equals(efFkAttr.Name, StringComparison.OrdinalIgnoreCase));

                string targetTable = null;
                string targetSchema = "dbo";
                string targetColumn = "Id";

                if (navProp != null)
                {
                    var (schema, table) = navProp.PropertyType.GetTableInfo();
                    targetTable = table;
                    targetSchema = string.IsNullOrWhiteSpace(schema) ? "dbo" : schema;

                    var pkProp = navProp.PropertyType
                        .GetProperties()
                        .FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() != null);
                    if (pkProp != null)
                        targetColumn = pkProp.Name;
                }

                var newFk = new ForeignKeyDefinition
                {
                    Column = columnName,
                    ReferencedTable = targetTable,
                    ReferencedSchema = targetSchema,
                    ReferencedColumn = targetColumn,
                    OnDelete = ReferentialAction.Cascade,
                    OnUpdate = ReferentialAction.NoAction,
                    ConstraintName = $"FK_{entityName}_{columnName}",
                    Description = description
                };

                if (!Exists(newFk))
                    foreignKeys.Add(newFk);

                continue;
            }

            if (prop.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
            {
                var baseName = prop.Name.Substring(0, prop.Name.Length - 2);
                var navProp = props.FirstOrDefault(p => p.Name.Equals(baseName, StringComparison.OrdinalIgnoreCase));

                if (navProp != null)
                {
                    var (schema, table) = navProp.PropertyType.GetTableInfo();
                    var targetTable = table;
                    var targetSchema = string.IsNullOrWhiteSpace(schema) ? "dbo" : schema;

                    var pkProp = navProp.PropertyType
                        .GetProperties()
                        .FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() != null);
                    var targetColumn = pkProp?.Name ?? "Id";

                    var newFk = new ForeignKeyDefinition
                    {
                        Column = columnName,
                        ReferencedTable = targetTable,
                        ReferencedSchema = targetSchema,
                        ReferencedColumn = targetColumn,
                        OnDelete = ReferentialAction.Cascade,
                        OnUpdate = ReferentialAction.NoAction,
                        ConstraintName = $"FK_{entityName}_{columnName}",
                        Description = description
                    };

                    if (!Exists(newFk))
                        foreignKeys.Add(newFk);
                }
            }
        }

        // 2️⃣ Infer from navigation properties
        foreach (var navProp in props)
        {
            var navType = navProp.PropertyType;
            string description = !string.IsNullOrWhiteSpace(navProp.GetCustomAttribute<DescriptionAttribute>()?.Description) ? navProp.GetCustomAttribute<DescriptionAttribute>()?.Description : null;


            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(navType) && navType != typeof(string))
                continue;
            if (navType == typeof(string))
                continue;

            var (refSchema, refTable) = navType.GetTableInfo();
            if (string.IsNullOrWhiteSpace(refTable))
                refTable = navType.Name;

            var fkPropName = $"{navProp.Name}Id";
            var fkProp = props.FirstOrDefault(p => p.Name.Equals(fkPropName, StringComparison.OrdinalIgnoreCase));

            if (fkProp == null)
            {
                fkProp = props.FirstOrDefault(p =>
                    p.GetCustomAttribute<ForeignKeyAttribute>()?.Name == navProp.Name);
            }

            if (fkProp == null)
                continue;

            var fkColumn = GetColumnName(fkProp);

            var pkProp = navType
                .GetProperties()
                .FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() != null);
            var refColumn = pkProp?.Name ?? "Id";

            var newFk = new ForeignKeyDefinition
            {
                Column = fkColumn,
                ReferencedTable = refTable,
                ReferencedSchema = string.IsNullOrWhiteSpace(refSchema) ? "dbo" : refSchema,
                ReferencedColumn = refColumn,
                OnDelete = ReferentialAction.Cascade,
                OnUpdate = ReferentialAction.NoAction,
                ConstraintName = $"FK_{entityName}_{fkColumn}",
                Description = description
            };

            if (!Exists(newFk))
                foreignKeys.Add(newFk);
        }

        return foreignKeys;
    }


    /// <summary>
    /// Sorts a list of <see cref="EntityDefinition"/> objects based on their foreign key dependencies.
    /// Ensures that referenced tables appear before dependent tables to avoid migration errors.
    /// </summary>
    /// <param name="entities">The list of entities to sort.</param>
    /// <returns>
    /// A new list of <see cref="EntityDefinition"/> objects sorted by dependency order.
    /// </returns>
    public static List<EntityDefinition> SortEntitiesByDependency(IEnumerable<EntityDefinition> entities)
    {
        var entityList = entities.ToList();
        var entityMap = entityList.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sorted = new List<EntityDefinition>();

        void Visit(EntityDefinition entity)
        {
            if (visited.Contains(entity.Name))
                return;

            visited.Add(entity.Name);

            foreach (var fk in entity.ForeignKeys)
            {
                if (entityMap.TryGetValue(fk.ReferencedTable, out var referencedEntity))
                {
                    Visit(referencedEntity);
                }
            }

            sorted.Add(entity);
        }

        foreach (var entity in entityList)
        {
            Visit(entity);
        }

        return sorted;
    }
    /// <summary>
    /// Infers foreign key relationships from navigation properties in the given CLR type.
    /// Looks for matching ID properties (e.g. Customer + CustomerId) and generates
    /// <see cref="ForeignKeyDefinition"/> entries if not already present.
    /// </summary>
    /// <param name="entityType">The CLR type representing the entity.</param>
    /// <param name="entity">The <see cref="EntityDefinition"/> being built.</param>
    private void InferForeignKeysFromNavigation(Type entityType, EntityDefinition entity)
    {
        var props = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var navProp in props)
        {
            var navType = navProp.PropertyType;

            // تخطي الـ Collections
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(navType) && navType != typeof(string))
                continue;

            // تخطي الـ Primitives والـ string
            if (!navType.IsClass || navType == typeof(string))
                continue;

            // جلب اسم الجدول والـ Schema من الكيان الهدف
            var (refSchema, refTable) = navType.GetTableInfo();

            // البحث عن العمود FK (مثال: CustomerId)
            var fkPropName = $"{navProp.Name}Id";
            var fkProp = props.FirstOrDefault(p => p.Name == fkPropName);

            // 🆕 دعم حالة PK=FK (مثال: UserProfile.Id مع [ForeignKey(nameof(User))])
            if (fkProp == null)
            {
                // لو العمود عليه ForeignKeyAttribute بيشاور على نفس الـ navProp
                fkProp = props.FirstOrDefault(p =>
                    p.GetCustomAttribute<ForeignKeyAttribute>()?.Name == navProp.Name);
            }

            if (fkProp == null)
                continue;

            var fkColumn = GetColumnName(fkProp);

            // تجنب التكرار
            if (entity.ForeignKeys.Any(fk => fk.Column == fkColumn))
                continue;

            // تحديد العمود المرجعي (PK) من الكيان الهدف
            var pkProp = navType
                .GetProperties()
                .FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() != null);

            var refColumn = pkProp != null ? pkProp.Name : "Id";

            // إضافة الـ FK
            entity.ForeignKeys.Add(new ForeignKeyDefinition
            {
                Column = fkColumn,
                ReferencedTable = refTable,
                ReferencedSchema = string.IsNullOrWhiteSpace(refSchema) ? "dbo" : refSchema,
                ReferencedColumn = refColumn,
                OnDelete = ReferentialAction.Cascade,
                OnUpdate = ReferentialAction.NoAction,
                ConstraintName = $"FK_{entity.Name}_{fkColumn}"
            });
        }
    }


    /// <summary>
    /// Infers collection-based relationships (One-to-Many and Many-to-Many)
    /// from navigation properties of type ICollection&lt;T&gt;.
    /// Adds foreign keys to target entities and registers relationship metadata.
    /// </summary>
    private void InferCollectionRelationships(Type entityType, EntityDefinition entity, List<EntityDefinition> allEntities)
    {
        Console.WriteLine($"[TRACE:Infer] Analyzing collections for entity: {entity.Name}");

        var props = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in props)
        {
            var propType = prop.PropertyType;

            if (!typeof(System.Collections.IEnumerable).IsAssignableFrom(propType) || propType == typeof(string))
                continue;

            var itemType = propType.IsGenericType ? propType.GetGenericArguments().FirstOrDefault() : null;
            if (itemType == null || !itemType.IsClass || itemType == typeof(string))
                continue;

            var (targetSchema, targetTable) = itemType.GetTableInfo();
            var targetEntity = ResolveEntity(allEntities, targetTable);
            if (targetEntity == null)
                continue;

            Console.WriteLine($"[TRACE:RelCheck] {entity.Name}.{prop.Name} -> {targetEntity.Name}");

            var reverseProps = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var reverseCollection = reverseProps.Any(p =>
                typeof(System.Collections.IEnumerable).IsAssignableFrom(p.PropertyType) &&
                p.PropertyType.IsGenericType &&
                p.PropertyType.GetGenericArguments().FirstOrDefault() == entityType);

            if (reverseCollection)
            {
                if (string.Compare(entity.Name, targetEntity.Name, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    Console.WriteLine($"[TRACE:SkipRelGen] Skipping duplicate Many-to-Many generation {entity.Name} <-> {targetEntity.Name}");
                    continue;
                }

                var joinTableName = $"{entity.Name}{targetEntity.Name}";
                Console.WriteLine($"[TRACE:JoinTable] Preparing join table: {joinTableName}");

                var existingJoinEntity = ResolveEntity(allEntities, joinTableName);
                var isExplicit = existingJoinEntity?.ClrType != null;

                entity.Relationships.Add(new RelationshipDefinition
                {
                    SourceEntity = entity.Name,
                    TargetEntity = targetEntity.Name,
                    SourceProperty = prop.Name,
                    TargetProperty = reverseProps
                        .FirstOrDefault(p => p.PropertyType.IsGenericType &&
                                             p.PropertyType.GetGenericArguments().FirstOrDefault() == entityType)?.Name,
                    SourceToTargetColumn = null,
                    Type = RelationshipType.ManyToMany,
                    JoinEntityName = joinTableName,
                    IsExplicitJoinEntity = isExplicit
                });

                if (!isExplicit)
                {
                    var leftClrType = entity.ClrType?.GetProperty("Id")?.PropertyType ?? typeof(Guid);
                    var rightClrType = targetEntity.ClrType?.GetProperty("Id")?.PropertyType ?? typeof(Guid);

                    var leftTypeName = leftClrType.MapClrTypeToSql();
                    var rightTypeName = rightClrType.MapClrTypeToSql();

                    Console.WriteLine($"[TRACE:JoinTable] Auto-generating shadow join table: {joinTableName}");

                    var shadowEntity = new EntityDefinition
                    {
                        Name = joinTableName,
                        Schema = entity.Schema,
                        ClrType = null,
                        IsShadowEntity = true,
                        Columns = new List<ColumnDefinition>
                    {
                        new ColumnDefinition { Name = $"{entity.Name}Id", PropertyType = leftClrType,  TypeName = leftTypeName,  IsNullable = false },
                        new ColumnDefinition { Name = $"{targetEntity.Name}Id", PropertyType = rightClrType, TypeName = rightTypeName, IsNullable = false }
                    },
                        PrimaryKey = new PrimaryKeyDefinition
                        {
                            Columns = new List<string> { $"{entity.Name}Id", $"{targetEntity.Name}Id" },
                            IsAutoGenerated = false,
                            Name = $"PK_{joinTableName}"
                        },
                        ForeignKeys = new List<ForeignKeyDefinition>()
                    };

                    // إضافة FK للطرف الأول مع فحص التكرار
                    var fk1 = new ForeignKeyDefinition
                    {
                        Column = $"{entity.Name}Id",
                        ReferencedTable = entity.Name,
                        ReferencedSchema = entity.Schema,
                        ConstraintName = $"FK_{joinTableName}_{entity.Name}Id"
                    };
                    if (!shadowEntity.ForeignKeys.Any(fk => fk.Column == fk1.Column && fk.ReferencedTable == fk1.ReferencedTable))
                        shadowEntity.ForeignKeys.Add(fk1);

                    // إضافة FK للطرف الثاني مع فحص التكرار
                    var fk2 = new ForeignKeyDefinition
                    {
                        Column = $"{targetEntity.Name}Id",
                        ReferencedTable = targetEntity.Name,
                        ReferencedSchema = targetEntity.Schema,
                        ConstraintName = $"FK_{joinTableName}_{targetEntity.Name}Id"
                    };
                    if (!shadowEntity.ForeignKeys.Any(fk => fk.Column == fk2.Column && fk.ReferencedTable == fk2.ReferencedTable))
                        shadowEntity.ForeignKeys.Add(fk2);

                    allEntities.Add(shadowEntity);
                }
                else
                {
                    // لو الجدول الوسيط Explicit ومفيش PK → نولده أوتوماتيك
                    if (existingJoinEntity != null &&
                        (existingJoinEntity.PrimaryKey == null || existingJoinEntity.PrimaryKey.Columns.Count == 0) &&
                        existingJoinEntity.ForeignKeys.Count >= 2)
                    {
                        var pkCols = existingJoinEntity.ForeignKeys.Select(fk => fk.Column).ToList();
                        existingJoinEntity.PrimaryKey = new PrimaryKeyDefinition
                        {
                            Columns = pkCols,
                            IsAutoGenerated = false,
                            Name = $"PK_{existingJoinEntity.Name}"
                        };
                        Console.WriteLine($"[TRACE:PK] Auto-assigning composite PK for explicit join entity '{existingJoinEntity.Name}': {string.Join(", ", pkCols)}");
                    }
                }

                continue;
            }

            // One-to-Many
            var expectedFkName = $"{entity.Name}Id";
            var hasFk = targetEntity.Columns.Any(c => c.Name.Equals(expectedFkName, StringComparison.OrdinalIgnoreCase));

            if (!hasFk)
            {
                var fkClrType = entity.ClrType?.GetProperty("Id")?.PropertyType ?? typeof(Guid);
                var fkTypeName = fkClrType.MapClrTypeToSql();

                Console.WriteLine($"[TRACE:FK] Adding FK column '{expectedFkName}' ({fkTypeName}) to '{targetEntity.Name}'");

                targetEntity.Columns.Add(new ColumnDefinition
                {
                    Name = expectedFkName,
                    PropertyType = fkClrType,
                    TypeName = fkTypeName,
                    IsNullable = false
                });

                var newFk = new ForeignKeyDefinition
                {
                    Column = expectedFkName,
                    ReferencedTable = entity.Name,
                    ReferencedSchema = entity.Schema,
                    ConstraintName = $"FK_{targetEntity.Name}_{expectedFkName}"
                };

                if (!targetEntity.ForeignKeys.Any(fk => fk.Column == newFk.Column && fk.ReferencedTable == newFk.ReferencedTable))
                    targetEntity.ForeignKeys.Add(newFk);
            }

            var reverseNav = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => p.PropertyType == entityType);

            entity.Relationships.Add(new RelationshipDefinition
            {
                SourceEntity = entity.Name,
                TargetEntity = targetEntity.Name,
                SourceProperty = prop.Name,
                TargetProperty = reverseNav?.Name,
                SourceToTargetColumn = expectedFkName,
                Type = RelationshipType.OneToMany
            });
        }
    }


    /// <summary>
    /// Infers one-to-one relationships between entities based on navigation properties.
    /// Detects matching foreign keys and primary key alignment to confirm uniqueness.
    /// Registers relationship metadata in <see cref="RelationshipDefinition"/>.
    /// </summary>
    /// <param name="entityType">The CLR type being analyzed.</param>
    /// <param name="entity">The <see cref="EntityDefinition"/> being built.</param>
    /// <param name="allEntities">All known entities for cross-reference.</param>
    public void InferOneToOneRelationships(Type clrType, EntityDefinition entity, List<EntityDefinition> allEntities)
    {
        Console.WriteLine($"[TRACE:OneToOne] Analyzing entity {entity.Name}");

        if (entity.ForeignKeys == null || entity.ForeignKeys.Count == 0)
        {
            Console.WriteLine("  No foreign keys found.");
            return;
        }

        foreach (var fk in entity.ForeignKeys)
        {
            Console.WriteLine($"  FK found: {fk.Column} -> {fk.ReferencedTable}");

            // البحث عن الكيان الهدف بالاسم فقط (Case-insensitive)
            var targetEntity = ResolveEntity(allEntities, fk.ReferencedTable);

            if (targetEntity == null)
                continue;

            bool alreadyHasNonOneToOne =
                entity.Relationships.Any(r => r.TargetEntity == targetEntity.Name && r.Type != RelationshipType.OneToOne) ||
                targetEntity.Relationships.Any(r => r.TargetEntity == entity.Name && r.Type != RelationshipType.OneToOne);

            if (alreadyHasNonOneToOne)
            {
                Console.WriteLine($"    Skipped 1:1: non-OneToOne relationship already exists between {entity.Name} and {targetEntity.Name}");
                continue;
            }

            bool isUnique = entity.UniqueConstraints.Any(u =>
                u.Columns.Count == 1 &&
                u.Columns.Contains(fk.Column, StringComparer.OrdinalIgnoreCase));

            bool isAlsoPrimaryKey = entity.PrimaryKey?.Columns?.Count == 1 &&
                                    entity.PrimaryKey.Columns.Contains(fk.Column, StringComparer.OrdinalIgnoreCase);

            bool hasRefToTarget = HasSingleReferenceNavigation(clrType, targetEntity.ClrType, out var sourceProp);
            bool targetHasRefBack = HasSingleReferenceNavigation(targetEntity.ClrType, clrType, out var targetProp);

            bool isStrictOneToOne = isUnique || isAlsoPrimaryKey;
            bool isNavOneToOne = hasRefToTarget && targetHasRefBack;

            if (!isStrictOneToOne && !isNavOneToOne)
            {
                Console.WriteLine("    Skipped 1:1: neither unique/PK nor mutual single navigations.");
                continue;
            }

            if (!isStrictOneToOne && isNavOneToOne)
            {
                var uqName = $"UQ_{entity.Name}_{fk.Column}";
                if (!entity.UniqueConstraints.Any(u => u.Name.Equals(uqName, StringComparison.OrdinalIgnoreCase)))
                {
                    entity.UniqueConstraints.Add(new UniqueConstraintDefinition
                    {
                        Name = uqName,
                        Columns = new List<string> { fk.Column },
                        Description = $"Auto-unique for 1:1 {entity.Name} → {targetEntity.Name}"
                    });
                    Console.WriteLine($"    ✅ Auto-added UNIQUE: {uqName}");
                }
            }

            var isRequired = sourceProp != null &&
                             clrType.GetProperty(sourceProp)?.GetCustomAttribute<RequiredAttribute>() != null;

            entity.Relationships.Add(new RelationshipDefinition
            {
                SourceEntity = entity.Name,
                TargetEntity = targetEntity.Name,
                SourceProperty = sourceProp ?? $"NavTo{targetEntity.Name}",
                TargetProperty = targetProp,
                SourceToTargetColumn = fk.Column,
                Type = RelationshipType.OneToOne,
                IsRequired = isRequired,
                OnDelete = fk.OnDelete
            });

            targetEntity.Relationships.Add(new RelationshipDefinition
            {
                SourceEntity = targetEntity.Name,
                TargetEntity = entity.Name,
                SourceProperty = targetProp ?? $"NavTo{entity.Name}",
                TargetProperty = sourceProp,
                SourceToTargetColumn = fk.Column,
                Type = RelationshipType.OneToOne,
                IsRequired = false,
                OnDelete = fk.OnDelete
            });

            Console.WriteLine($"    ✅ OneToOne relationship added: {entity.Name}.{sourceProp} <-> {targetEntity.Name}.{targetProp}");
        }
    }

    /// <summary>
    /// Detects if the type has a single reference navigation to another type (non-collection, non-string).
    /// </summary>
    private static bool HasSingleReferenceNavigation(Type from, Type to, out string? propName)
    {
        var props = from.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.PropertyType == to)
                    .ToList();

        propName = props.Count == 1 ? props[0].Name : null;
        return props.Count == 1;
    }

    /// <summary>
    /// Infers CHECK constraints from validation attributes more broadly:
    /// - [StringLength], [MaxLength], [MinLength]
    /// - [Range] for numeric types
    /// - [Required] for any type (string/non-string)
    /// </summary>
    public void InferCheckConstraints(Type clrType, EntityDefinition entity)
    {
        Console.WriteLine($"[TRACE:CheckConstraints] Analyzing entity {entity.Name}");

        bool AlreadyHasConstraint(string expr) =>
            entity.CheckConstraints.Any(c => c.Expression.Equals(expr, StringComparison.OrdinalIgnoreCase));

        // 🥇 PK: Not Null فقط، بدون إعادة تفعيل Identity
        if (entity.PrimaryKey?.Columns != null)
        {
            foreach (var pkColName in entity.PrimaryKey.Columns)
            {
                var col = entity.Columns.FirstOrDefault(c =>
                    c.Name.Equals(pkColName, StringComparison.OrdinalIgnoreCase));
                if (col != null)
                {
                    col.IsNullable = false;

                    Console.WriteLine($"    [TRACE:CheckConstraints] PK {col.Name} → Identity={col.IsIdentity} (before)");

                    if (!col.IsIdentity)
                        Console.WriteLine($"    ⚠ Identity remains false for {col.Name} (composite PK)");
                    else
                        Console.WriteLine($"    ✅ PK {col.Name}: Not Null + Identity");
                }
            }
        }

        // 🥈 تحليل كل الأعمدة
        foreach (var prop in clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var colName = GetColumnName(prop);
            var col = entity.Columns.FirstOrDefault(c =>
                c.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));
            if (col == null) continue;

            // 1) قيد أساسي من Nullability + نوع العمود
            if (!col.IsNullable)
            {
                string expr = IsStringColumn(col)
                    ? $"LEN([{col.Name}]) > 0"
                    : $"[{col.Name}] IS NOT NULL";

                if (!AlreadyHasConstraint(expr))
                {
                    entity.CheckConstraints.Add(new CheckConstraintDefinition
                    {
                        Name = $"CK_{entity.Name}_{col.Name}_NotNull",
                        Expression = expr,
                        Description = $"{col.Name} must not be NULL or empty",
                        ReferencedColumns = new List<string> { col.Name }
                    });
                    Console.WriteLine($"    ✅ Added CHECK (NotNull/NotEmpty) on {col.Name}");
                }
            }

            // 2) من Attributes
            var strLenAttr = prop.GetCustomAttribute<StringLengthAttribute>();
            if (strLenAttr?.MaximumLength > 0)
            {
                var expr = $"LEN([{col.Name}]) <= {strLenAttr.MaximumLength}";
                if (!AlreadyHasConstraint(expr))
                {
                    entity.CheckConstraints.Add(new CheckConstraintDefinition
                    {
                        Name = $"CK_{entity.Name}_{col.Name}_MaxLen",
                        Expression = expr,
                        Description = $"Max length of {col.Name} is {strLenAttr.MaximumLength} characters",
                        ReferencedColumns = new List<string> { col.Name }
                    });
                    Console.WriteLine($"    ✅ Added CHECK (StringLength) on {col.Name}");
                }
            }

            var maxLenAttr = prop.GetCustomAttribute<MaxLengthAttribute>();
            if (maxLenAttr?.Length > 0)
            {
                var expr = $"LEN([{col.Name}]) <= {maxLenAttr.Length}";
                if (!AlreadyHasConstraint(expr))
                {
                    entity.CheckConstraints.Add(new CheckConstraintDefinition
                    {
                        Name = $"CK_{entity.Name}_{col.Name}_MaxLen",
                        Expression = expr,
                        Description = $"Max length of {col.Name} is {maxLenAttr.Length} characters",
                        ReferencedColumns = new List<string> { col.Name }
                    });
                    Console.WriteLine($"    ✅ Added CHECK (MaxLength) on {col.Name}");
                }
            }

            var minLenAttr = prop.GetCustomAttribute<MinLengthAttribute>();
            if (minLenAttr?.Length > 0)
            {
                var expr = $"LEN([{col.Name}]) >= {minLenAttr.Length}";
                if (!AlreadyHasConstraint(expr))
                {
                    entity.CheckConstraints.Add(new CheckConstraintDefinition
                    {
                        Name = $"CK_{entity.Name}_{col.Name}_MinLen",
                        Expression = expr,
                        Description = $"Min length of {col.Name} is {minLenAttr.Length} characters",
                        ReferencedColumns = new List<string> { col.Name }
                    });
                    Console.WriteLine($"    ✅ Added CHECK (MinLength) on {col.Name}");
                }
            }

            // 
            var rangeAttr = prop.GetCustomAttribute<RangeAttribute>();
            if (rangeAttr?.Minimum != null && rangeAttr.Maximum != null)
            {
                string expr;

                if (rangeAttr.Minimum is DateTime minDate && rangeAttr.Maximum is DateTime maxDate)
                {
                    expr = $"[{col.Name}] BETWEEN '{minDate:yyyy-MM-dd}' AND '{maxDate:yyyy-MM-dd}'";
                }
                else
                {
                    expr = $"[{col.Name}] BETWEEN {rangeAttr.Minimum} AND {rangeAttr.Maximum}";
                }

                if (!AlreadyHasConstraint(expr))
                {
                    entity.CheckConstraints.Add(new CheckConstraintDefinition
                    {
                        Name = $"CK_{entity.Name}_{col.Name}_Range",
                        Expression = expr,
                        Description = $"{col.Name} must be between {rangeAttr.Minimum} and {rangeAttr.Maximum}",
                        ReferencedColumns = new List<string> { col.Name }
                    });
                    Console.WriteLine($"    ✅ Added CHECK (Range) on {col.Name}");
                }
            }

            var requiredAttr = prop.GetCustomAttribute<RequiredAttribute>();
            if (requiredAttr != null)
            {
                string expr = IsStringColumn(col)
                    ? $"LEN([{col.Name}]) > 0"
                    : $"[{col.Name}] IS NOT NULL";

                if (!AlreadyHasConstraint(expr))
                {
                    entity.CheckConstraints.Add(new CheckConstraintDefinition
                    {
                        Name = $"CK_{entity.Name}_{col.Name}_Required",
                        Expression = expr,
                        Description = $"{col.Name} is required",
                        ReferencedColumns = new List<string> { col.Name }
                    });
                    Console.WriteLine($"    ✅ Added CHECK (Required) on {col.Name}");
                }
            }

            var regexAttr = prop.GetCustomAttribute<RegularExpressionAttribute>();
            if (regexAttr != null && !string.IsNullOrWhiteSpace(regexAttr.Pattern))
            {
                // ملاحظة: SQL لا يدعم regex مباشرة، نستخدم LIKE لو ممكن
                if (regexAttr.Pattern.StartsWith("^") && regexAttr.Pattern.EndsWith("$") &&
                    !regexAttr.Pattern.Contains(".*") && !regexAttr.Pattern.Contains("\\"))
                {
                    var likeExpr = regexAttr.Pattern.Trim('^', '$').Replace(".", "_");
                    var expr = $"[{col.Name}] LIKE '{likeExpr}'";

                    if (!AlreadyHasConstraint(expr))
                    {
                        entity.CheckConstraints.Add(new CheckConstraintDefinition
                        {
                            Name = $"CK_{entity.Name}_{col.Name}_Regex",
                            Expression = expr,
                            Description = $"{col.Name} must match pattern {regexAttr.Pattern}",
                            ReferencedColumns = new List<string> { col.Name }
                        });
                        Console.WriteLine($"    ✅ Added CHECK (Regex-LIKE) on {col.Name}");
                    }
                }
                else
                {
                    Console.WriteLine($"    ⚠ Skipped Regex CHECK on {col.Name}: pattern too complex for SQL LIKE");
                }
            }
        }
    }

    /// <summary>
    /// Detects string SQL types, covering sizes and (max).
    /// </summary>
    private bool IsStringColumn(ColumnDefinition col)
    {
        if (string.IsNullOrEmpty(col.TypeName)) return false;
        var t = col.TypeName.Split('(')[0].Trim().ToLowerInvariant();
        return t == "nvarchar" || t == "varchar" ||
               t == "char" || t == "nchar" ||
               t == "text" || t == "ntext";
    }


    public static bool IsIndexableExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return false;

        var indexableFunctions = new[]
        {
        "LEN(", "UPPER(", "LOWER(", "LTRIM(", "RTRIM(",
        "YEAR(", "MONTH(", "DAY(", "DATEPART(", "ISNULL("
    };

        return indexableFunctions.Any(f =>
            expression.Contains(f, StringComparison.OrdinalIgnoreCase));
    }

    public static string? ExtractColumnFromExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return null;

        var match = Regex.Match(expression, @"\[(\w+)\]");
        return match.Success ? match.Groups[1].Value : null;
    }

    private bool IsNavigationProperty(PropertyInfo prop)
    {
        var type = prop.PropertyType;

        // ✅ أنواع SQL المعروفة
        var sqlTypes = new[]
        {
        typeof(string), typeof(int), typeof(long), typeof(short),
        typeof(decimal), typeof(double), typeof(float),
        typeof(bool), typeof(DateTime), typeof(Guid), typeof(byte[])
    };

        if (sqlTypes.Contains(type))
            return false;

        // ❌ لو النوع كلاس أو مجموعة، نعتبره تنقلي
        if (type.IsClass || typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
            return true;

        return false;
    }


    private void ResolveForeignKeys(List<EntityDefinition> entities)
    {
        foreach (var entity in entities)
        {
            foreach (var fk in entity.ForeignKeys)
            {
                if (string.IsNullOrWhiteSpace(fk.ReferencedTable))
                {
                    var targetEntity = entities.FirstOrDefault(e =>
                        e.Columns.Any(c =>
                            c.Name.Equals(fk.ReferencedColumn, StringComparison.OrdinalIgnoreCase)));

                    if (targetEntity != null)
                    {
                        fk.ReferencedTable = targetEntity.Name;

                        if (string.IsNullOrWhiteSpace(fk.ReferencedColumn) || fk.ReferencedColumn == "Id")
                        {
                            var pkCol = targetEntity.Columns.FirstOrDefault(c => c.IsPrimaryKey);
                            if (pkCol != null)
                                fk.ReferencedColumn = pkCol.Name;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// يبني قائمة المفاتيح الأجنبية (Foreign Keys) لكيان معين.
    /// يعتمد على EF Core ForeignKeyAttribute أو الاستنتاج الذاتي من أسماء الأعمدة والـ Navigation.
    /// </summary>
    /// <summary>
    /// يبني قائمة المفاتيح الأجنبية (Foreign Keys) لكيان معين.
    /// يعتمد على EF Core ForeignKeyAttribute أو الاستنتاج الذاتي من أسماء الأعمدة والـ Navigation.
    /// ويكمل بيانات الجدول والعمود الهدف باستخدام Reflection على نوع الـ Navigation.
    /// </summary>
    internal static List<ForeignKeyDefinition> BuildForeignKeys(Type entityType, string entityName)
    {
        var foreignKeys = new List<ForeignKeyDefinition>();

        foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            // 1️⃣ حالة وجود ForeignKeyAttribute من EF Core
            var efFkAttr = prop.GetCustomAttribute<ForeignKeyAttribute>();
            if (efFkAttr != null)
            {
                // نحاول نلاقي الـ Navigation Property اللي بيشاور عليها الـ Attribute
                var navProp = entityType.GetProperties()
                    .FirstOrDefault(p => p.Name.Equals(efFkAttr.Name, StringComparison.OrdinalIgnoreCase));

                string targetTable = null;
                string targetSchema = "dbo";
                string targetColumn = "Id";

                if (navProp != null)
                {
                    var tableAttr = navProp.PropertyType.GetCustomAttribute<TableAttribute>();
                    targetTable = tableAttr != null ? tableAttr.Name : navProp.PropertyType.Name;

                    if (tableAttr != null && !string.IsNullOrWhiteSpace(tableAttr.Schema))
                        targetSchema = tableAttr.Schema;

                    var pkProp = navProp.PropertyType
                        .GetProperties()
                        .FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() != null);

                    if (pkProp != null)
                        targetColumn = pkProp.Name;
                }

                foreignKeys.Add(new ForeignKeyDefinition
                {
                    Column = prop.Name,
                    ReferencedTable = targetTable,
                    ReferencedSchema = targetSchema,
                    ReferencedColumn = targetColumn,
                    OnDelete = ReferentialAction.Cascade,
                    OnUpdate = ReferentialAction.NoAction,
                    ConstraintName = $"FK_{entityName}_{prop.Name}"
                });
                continue;
            }

            // 2️⃣ حالة اسم العمود بينتهي بـ Id (مثال: CustomerId)
            if (prop.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
            {
                var baseName = prop.Name.Substring(0, prop.Name.Length - 2);

                // نحاول نلاقي Navigation Property بنفس الاسم
                var navProp = entityType.GetProperties()
                    .FirstOrDefault(p => p.Name.Equals(baseName, StringComparison.OrdinalIgnoreCase));

                if (navProp != null)
                {
                    var tableAttr = navProp.PropertyType.GetCustomAttribute<TableAttribute>();
                    var targetTable = tableAttr != null ? tableAttr.Name : navProp.PropertyType.Name;

                    var targetSchema = tableAttr != null && !string.IsNullOrWhiteSpace(tableAttr.Schema)
                        ? tableAttr.Schema
                        : "dbo";

                    var pkProp = navProp.PropertyType
                        .GetProperties()
                        .FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() != null);

                    var targetColumn = pkProp != null ? pkProp.Name : "Id";

                    foreignKeys.Add(new ForeignKeyDefinition
                    {
                        Column = prop.Name,
                        ReferencedTable = targetTable,
                        ReferencedSchema = targetSchema,
                        ReferencedColumn = targetColumn,
                        OnDelete = ReferentialAction.Cascade,
                        OnUpdate = ReferentialAction.NoAction,
                        ConstraintName = $"FK_{entityName}_{prop.Name}"
                    });
                }
            }
        }

        return foreignKeys;
    }



    public static List<IndexDefinition> AddCheckConstraintIndexes(EntityDefinition entity)
    {
        var result = new List<IndexDefinition>();

        foreach (var ck in entity.CheckConstraints)
        {
            foreach (var colName in ck.ReferencedColumns)
            {
                bool alreadyIndexed = entity.Indexes.Any(ix =>
                    ix.Columns.Contains(colName, StringComparer.OrdinalIgnoreCase));

                var colDef = entity.Columns.FirstOrDefault(c =>
                    c.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));

                if (colDef == null)
                    continue;

                // 🛡️ شرط الحجم والنوع
                if (colDef.TypeName.Contains("max", StringComparison.OrdinalIgnoreCase) ||
                    colDef.TypeName.Contains("text", StringComparison.OrdinalIgnoreCase) ||
                    colDef.TypeName.Contains("ntext", StringComparison.OrdinalIgnoreCase) ||
                    colDef.TypeName.Contains("image", StringComparison.OrdinalIgnoreCase) ||
                    (colDef.TypeName.StartsWith("nvarchar", StringComparison.OrdinalIgnoreCase) &&
                     int.TryParse(new string(colDef.TypeName.Where(char.IsDigit).ToArray()), out var len) &&
                     len > 450))
                {
                    Console.WriteLine($"⚠️ Skipped auto-index for {colName} → type {colDef.TypeName} not indexable");
                    continue;
                }

                // 🛡️ شرط الاعتماد على الـ Index من EF (اختياري)
                bool hasEfIndex = entity.Indexes.Any(ix =>
                    ix.Columns.Contains(colName, StringComparer.OrdinalIgnoreCase) &&
                    !ix.Name.EndsWith("_ForCheck", StringComparison.OrdinalIgnoreCase));
                if (!hasEfIndex)
                {
                    Console.WriteLine($"⏩ Skipped auto-index for {colName} → No EF index defined");
                    continue;
                }

                // ✅ إضافة الفهرس لو مش موجود
                if (!alreadyIndexed)
                {
                    result.Add(new IndexDefinition
                    {
                        Name = $"IX_{entity.Name}_{colName}_ForCheck",
                        Columns = new List<string> { colName },
                        IsUnique = false,
                        Description = $"Auto index to support CHECK constraint {ck.Name}"
                    });
                    Console.WriteLine($"📌 Auto-index added for CHECK: IX_{entity.Name}_{colName}_ForCheck");
                }
            }
        }

        return result;
    }


    public static List<IndexDefinition> AddSensitiveIndexes(EntityDefinition entity)
    {
        var result = new List<IndexDefinition>();
        var sensitiveNames = new[] { "Email", "Username", "Code" };

        foreach (var col in entity.Columns)
        {
            if (sensitiveNames.Contains(col.Name, StringComparer.OrdinalIgnoreCase))
            {
                bool alreadyIndexed = entity.Indexes.Any(ix =>
                    ix.Columns.Contains(col.Name, StringComparer.OrdinalIgnoreCase));

                if (!alreadyIndexed)
                {
                    result.Add(new IndexDefinition
                    {
                        Name = $"IX_{entity.Name}_{col.Name}_AutoSensitive",
                        Columns = new List<string> { col.Name },
                        IsUnique = true,
                        Description = "Auto-generated index for login-critical field"
                    });
                }
            }
        }

        return result;
    }

    public static List<IndexDefinition> AddNavigationIndexes(EntityDefinition entity)
    {
        var result = new List<IndexDefinition>();

        foreach (var rel in entity.Relationships)
        {
            var fkColumn = rel.SourceToTargetColumn ?? $"{rel.TargetEntity}Id";

            bool alreadyIndexed = entity.Indexes.Any(ix =>
                ix.Columns.Contains(fkColumn, StringComparer.OrdinalIgnoreCase));

            bool isColumnValid = entity.Columns.Any(c =>
                c.Name.Equals(fkColumn, StringComparison.OrdinalIgnoreCase));

            if (!alreadyIndexed && isColumnValid)
            {
                result.Add(new IndexDefinition
                {
                    Name = $"IX_{entity.Name}_{fkColumn}_AutoNav",
                    Columns = new List<string> { fkColumn },
                    IsUnique = false,
                    Description = "Auto-generated index for navigation property"
                });
            }
        }

        return result;
    }

    /// <summary>
    /// يحاول إيجاد الكيان الهدف في قائمة الكيانات باستخدام اسم الجدول فقط.
    /// </summary>
    private EntityDefinition? ResolveEntity(IEnumerable<EntityDefinition> allEntities, string referencedTable)
    {
        if (string.IsNullOrWhiteSpace(referencedTable))
            return null;

        var target = allEntities.FirstOrDefault(e =>
            string.Equals(e.Name, referencedTable, StringComparison.OrdinalIgnoreCase));

        if (target == null)
        {
            Console.WriteLine($"    Target entity not found in allEntities for table: {referencedTable}");
            Console.WriteLine("[REL] Available entities: " +
                string.Join(", ", allEntities.Select(e => $"[{e.Schema}].[{e.Name}]")));
        }

        return target;
    }

    private void InferCheckConstraintsFromColumns(EntityDefinition entity)
    {
        foreach (var col in entity.Columns)
        {
            if (col.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
            {
                entity.CheckConstraints.Add(new CheckConstraintDefinition
                {
                    Name = $"CK_{entity.Name}_{col.Name}_NotEmpty",
                    Expression = $"[{col.Name}] <> '00000000-0000-0000-0000-000000000000'",
                    Description = $"{col.Name} must not be an empty GUID"
                });
            }
        }
    }

    ///// <summary>
    ///// Helper method to check if the SQL column type is a string type (covers sizes and max).
    ///// </summary>
    //private bool IsStringColumn(ColumnDefinition col)
    //{
    //    var t = col.TypeName?.Trim().ToLowerInvariant();
    //    return t.StartsWith("nvarchar") ||
    //           t.StartsWith("varchar") ||
    //           t.StartsWith("char") ||
    //           t.StartsWith("nchar") ||
    //           t.StartsWith("text") ||
    //           t.StartsWith("ntext");
    //}

}
