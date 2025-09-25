using Syn.Core.SqlSchemaGenerator.Helper;
using Syn.Core.SqlSchemaGenerator.Models;

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace Syn.Core.SqlSchemaGenerator
{
    /// <summary>
    /// Reads SQL Server database schema into <see cref="EntityDefinition"/> objects.
    /// Uses a unified read pipeline and splits output into separate collections.
    /// </summary>
    public class DatabaseSchemaReader
    {
        private readonly DbConnection _connection;

        public DatabaseSchemaReader(DbConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public EntityDefinition GetEntityDefinition(string schemaName, string tableName)
        {
            if (_connection.State != ConnectionState.Open)
                _connection.Open();

            var entity = new EntityDefinition
            {
                Schema = schemaName,
                Name = tableName,
                Columns = new List<ColumnDefinition>(),
                Indexes = new List<IndexDefinition>(),
                Constraints = new List<ConstraintDefinition>(),      // PK/FK/Unique
                CheckConstraints = new List<CheckConstraintDefinition>() // CHECK
            };

            ReadColumns(entity);
            if (!entity.Columns.Any())
                return null;

            ReadIndexes(entity);
            ReadConstraintsAndChecks(entity);

            return entity;
        }

        public List<EntityDefinition> GetAllEntities()
        {
            if (_connection.State != ConnectionState.Open)
                _connection.Open();

            var results = new List<EntityDefinition>();

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                SELECT TABLE_SCHEMA, TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var def = GetEntityDefinition(reader.GetString(0), reader.GetString(1));
                if (def != null) results.Add(def);
            }

            return results;
        }
        /// <summary>
        /// Loads the current EntityDefinition from the database for a given EntityDefinition (new model).
        /// Uses Schema and Table name from the entity itself.
        /// If the table is missing, returns an empty placeholder to treat it as new.
        /// </summary>
        public EntityDefinition LoadEntityFromDatabase(Type type)
        {
            var (schema, table) = type.GetTableInfo();
            Console.WriteLine($"[DB Loader] Loading schema for table [{schema}].[{table}]");

            var entity = GetEntityDefinition(schema, table);

            if (entity == null)
            {
                Console.WriteLine($"?? [DB Loader] Table [{schema}].[{table}] not found in DB. Marked as NEW.");
                return new EntityDefinition
                {
                    Schema = schema,
                    Name = table,
                    Columns = entity.Columns,
                    Constraints = entity.Constraints,
                    CheckConstraints = entity.CheckConstraints,
                    Indexes = entity.Indexes
                };
            }

            return entity;
        }

        /// <summary>
        /// Loads the current EntityDefinition from the database for a given EntityDefinition (new model).
        /// Uses Schema and Table name from the entity itself.
        /// If the table is missing, returns an empty placeholder to treat it as new.
        /// </summary>
        public EntityDefinition LoadEntityFromDatabase(EntityDefinition newEntity)
        {
            var schema = newEntity.Schema;
            var table = newEntity.Name;

            Console.WriteLine($"[DB Loader] Loading schema for table [{schema}].[{table}]");

            var entity = GetEntityDefinition(schema, table);

            if (entity == null)
            {
                Console.WriteLine($"?? [DB Loader] Table [{schema}].[{table}] not found in DB. Marked as NEW.");
                return new EntityDefinition
                {
                    Schema = schema,
                    Name = table,
                    Columns = entity.Columns,
                    Constraints = entity.Constraints,
                    CheckConstraints = entity.CheckConstraints,
                    Indexes = entity.Indexes
                };
            }

            return entity;
        }

        // ===== Private unified readers =====

        private void ReadColumns(EntityDefinition entity)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
SELECT  
    c.COLUMN_NAME,  
    c.DATA_TYPE,  
    c.CHARACTER_MAXIMUM_LENGTH,
    c.IS_NULLABLE,  
    c.COLUMN_DEFAULT,
    col.is_identity
FROM INFORMATION_SCHEMA.COLUMNS c
JOIN sys.objects o  
    ON o.object_id = OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME))
JOIN sys.columns col  
    ON col.object_id = o.object_id
    AND col.name = c.COLUMN_NAME
JOIN sys.schemas s  
    ON s.schema_id = o.schema_id
WHERE c.TABLE_SCHEMA = @schema  
  AND c.TABLE_NAME = @table
  AND o.type = 'U' -- جداول فقط
ORDER BY c.ORDINAL_POSITION";

            AddParam(cmd, "@schema", entity.Schema);
            AddParam(cmd, "@table", entity.Name);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var colName = reader.GetString(0);
                var dataType = reader.GetString(1);
                var charMaxLen = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                var isNullable = reader.GetString(3) == "YES";
                var defaultValue = reader.IsDBNull(4) ? null : reader.GetValue(4);
                var isIdentity = !reader.IsDBNull(5) && reader.GetBoolean(5);

                // 🛠️ إعادة بناء TypeName مع الطول
                string typeName;
                if (charMaxLen.HasValue)
                {
                    if (charMaxLen.Value == -1)
                        typeName = $"{dataType}(max)";
                    else
                        typeName = $"{dataType}({charMaxLen.Value})";
                }
                else
                {
                    typeName = dataType;
                }

                // ✅ تتبع واضح لكل عمود
                Console.WriteLine($"[TRACE:ColumnInit] {entity.Name}.{colName} → Identity={isIdentity}, Nullable={isNullable}, Type={typeName}");

                entity.Columns.Add(new ColumnDefinition
                {
                    Name = colName,
                    TypeName = typeName,
                    IsNullable = isNullable,
                    DefaultValue = defaultValue,
                    IsIdentity = isIdentity
                });
            }
        }

        private void ReadIndexes(EntityDefinition entity)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                SELECT i.name AS IndexName,
                       i.is_unique,
                       c.name AS ColumnName
                FROM sys.indexes i
                INNER JOIN sys.index_columns ic 
                    ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                INNER JOIN sys.columns c 
                    ON ic.object_id = c.object_id AND c.column_id = ic.column_id
                INNER JOIN sys.objects o 
                    ON i.object_id = o.object_id
                INNER JOIN sys.schemas s 
                    ON o.schema_id = s.schema_id
                WHERE o.name = @table AND s.name = @schema
                      AND i.is_primary_key = 0
                ORDER BY i.name, ic.key_ordinal";

            AddParam(cmd, "@schema", entity.Schema);
            AddParam(cmd, "@table", entity.Name);

            using var reader = cmd.ExecuteReader();
            var indexGroups = new Dictionary<string, IndexDefinition>();

            while (reader.Read())
            {
                var idxName = reader.GetString(0);
                if (!indexGroups.TryGetValue(idxName, out var idxDef))
                {
                    idxDef = new IndexDefinition
                    {
                        Name = idxName,
                        IsUnique = reader.GetBoolean(1),
                        Columns = new List<string>()
                    };
                    indexGroups[idxName] = idxDef;
                    entity.Indexes.Add(idxDef);
                }
                idxDef.Columns.Add(reader.GetString(2));
            }
        }

        /// <summary>
        /// Reads all relational and check constraints for the given table
        /// and populates the corresponding collections in the <see cref="EntityDefinition"/>.
        /// 
        /// Populates:
        /// - <see cref="EntityDefinition.Constraints"/> with:
        ///   * PRIMARY KEY constraints
        ///   * UNIQUE constraints
        ///   * FOREIGN KEY constraints (including referenced table and columns)
        /// - <see cref="EntityDefinition.CheckConstraints"/> with:
        ///   * CHECK constraints and their expressions
        /// </summary>
        /// <param name="entity">
        /// The <see cref="EntityDefinition"/> to populate.
        /// Must have <see cref="EntityDefinition.Schema"/> and <see cref="EntityDefinition.Name"/> set.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="entity"/> is null.
        /// </exception>
        internal void ReadConstraintsAndChecks(EntityDefinition entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            // ===== 1) Read PK / UNIQUE constraints =====
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
SELECT  
    tc.CONSTRAINT_NAME,  
    tc.CONSTRAINT_TYPE,  
    kcu.COLUMN_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
    ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
WHERE tc.TABLE_SCHEMA = @schema  
  AND tc.TABLE_NAME = @table
  AND tc.CONSTRAINT_TYPE IN ('PRIMARY KEY', 'UNIQUE')
ORDER BY tc.CONSTRAINT_NAME, kcu.ORDINAL_POSITION";

                AddParam(cmd, "@schema", entity.Schema);
                AddParam(cmd, "@table", entity.Name);

                using var reader = cmd.ExecuteReader();
                var groups = new Dictionary<string, ConstraintDefinition>();

                while (reader.Read())
                {
                    var name = reader.GetString(0);
                    var type = reader.GetString(1);
                    var col = reader.GetString(2);

                    if (!groups.TryGetValue(name, out var def))
                    {
                        def = new ConstraintDefinition
                        {
                            Name = name,
                            Type = type,
                            Columns = new List<string>(),
                            ReferencedColumns = new List<string>()
                        };
                        groups[name] = def;
                        entity.Constraints.Add(def);

                        if (type.Equals("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
                        {
                            entity.PrimaryKey = new PrimaryKeyDefinition
                            {
                                Name = name,
                                Columns = def.Columns
                            };
                        }
                    }

                    def.Columns.Add(col);
                    def.ReferencedColumns.Add(col);
                }
            }

            // ===== 2) Read FOREIGN KEY constraints =====
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
SELECT  
    fk.name AS ConstraintName,
    parent_col.name AS ColumnName,
    ref_schema.name AS RefSchema,
    ref_table.name  AS RefTable,
    ref_col.name    AS RefColumn,
    fk.delete_referential_action,
    fk.update_referential_action
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc
    ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.tables parent_table
    ON parent_table.object_id = fk.parent_object_id
INNER JOIN sys.schemas parent_schema
    ON parent_schema.schema_id = parent_table.schema_id
INNER JOIN sys.columns parent_col
    ON parent_col.object_id = parent_table.object_id
    AND parent_col.column_id = fkc.parent_column_id
INNER JOIN sys.tables ref_table
    ON ref_table.object_id = fk.referenced_object_id
INNER JOIN sys.schemas ref_schema
    ON ref_schema.schema_id = ref_table.schema_id
INNER JOIN sys.columns ref_col
    ON ref_col.object_id = ref_table.object_id
    AND ref_col.column_id = fkc.referenced_column_id
WHERE parent_schema.name = @schema
  AND parent_table.name = @table
ORDER BY fk.name, fkc.constraint_column_id";

                AddParam(cmd, "@schema", entity.Schema);
                AddParam(cmd, "@table", entity.Name);

                using var reader = cmd.ExecuteReader();
                var groups = new Dictionary<string, ConstraintDefinition>();

                while (reader.Read())
                {
                    var name = reader.GetString(0);
                    var col = reader.GetString(1);
                    var refSchema = reader.GetString(2);
                    var refTable = reader.GetString(3);
                    var refCol = reader.GetString(4);
                    var onDelete = (ReferentialAction)reader.GetByte(5);
                    var onUpdate = (ReferentialAction)reader.GetByte(6);

                    if (!groups.TryGetValue(name, out var def))
                    {
                        def = new ConstraintDefinition
                        {
                            Name = name,
                            Type = "FOREIGN KEY",
                            Columns = new List<string>(),
                            ReferencedSchema = refSchema,
                            ReferencedTable = refTable,
                            ReferencedColumns = new List<string>(),
                            OnDelete = onDelete,
                            OnUpdate = onUpdate
                        };
                        groups[name] = def;
                        entity.Constraints.Add(def);
                    }

                    def.Columns.Add(col);
                    def.ReferencedColumns.Add(refCol);
                }
            }

            // ===== 3) Read CHECK constraints =====
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
SELECT  
    cc.CONSTRAINT_NAME,  
    cc.CHECK_CLAUSE
FROM INFORMATION_SCHEMA.CHECK_CONSTRAINTS cc
INNER JOIN INFORMATION_SCHEMA.CONSTRAINT_TABLE_USAGE tcu
    ON cc.CONSTRAINT_NAME = tcu.CONSTRAINT_NAME
WHERE tcu.TABLE_SCHEMA = @schema  
  AND tcu.TABLE_NAME = @table";

                AddParam(cmd, "@schema", entity.Schema);
                AddParam(cmd, "@table", entity.Name);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var name = reader.GetString(0);
                    var expr = reader.GetString(1);

                    var referencedCols = ExtractReferencedColumns(expr, entity);

                    // نسخة ConstraintDefinition
                    entity.Constraints.Add(new ConstraintDefinition
                    {
                        Name = name,
                        Type = "CHECK",
                        Columns = new List<string>(),
                        ReferencedColumns = referencedCols,
                        DefaultValue = expr
                    });

                    // نسخة CheckConstraintDefinition
                    entity.CheckConstraints.Add(new CheckConstraintDefinition
                    {
                        Name = name,
                        Expression = expr,
                        ReferencedColumns = referencedCols
                    });
                }

            }

            // ===== 4) Read DEFAULT constraints =====
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
SELECT  
    dc.name AS ConstraintName,
    c.name AS ColumnName,
    dc.definition AS DefaultValue
FROM sys.default_constraints dc
INNER JOIN sys.columns c 
    ON c.object_id = dc.parent_object_id 
    AND c.column_id = dc.parent_column_id
INNER JOIN sys.objects o
    ON o.object_id = dc.parent_object_id
INNER JOIN sys.schemas s
    ON s.schema_id = o.schema_id
WHERE s.name = @schema
  AND o.name = @table";

                AddParam(cmd, "@schema", entity.Schema);
                AddParam(cmd, "@table", entity.Name);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var name = reader.GetString(0);
                    var col = reader.GetString(1);
                    var val = reader.GetString(2);

                    entity.Constraints.Add(new ConstraintDefinition
                    {
                        Name = name,
                        Type = "DEFAULT",
                        Columns = new List<string> { col },
                        ReferencedColumns = new List<string> { col },
                        DefaultValue = val
                    });
                }
            }

            // ===== 5) Read Extended Properties (Descriptions) =====
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
SELECT  
    con.name AS ConstraintName,
    CAST(ep.value AS NVARCHAR(MAX)) AS Description
FROM sys.extended_properties ep
INNER JOIN sys.objects con
    ON ep.major_id = con.object_id
WHERE ep.name = N'MS_Description'
  AND con.parent_object_id = OBJECT_ID(@schema + '.' + @table)";

                AddParam(cmd, "@schema", entity.Schema);
                AddParam(cmd, "@table", entity.Name);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var constraintName = reader.GetString(0);
                    var description = reader.IsDBNull(1) ? null : reader.GetString(1);

                    var constDef = entity.Constraints
                        .FirstOrDefault(c => c.Name.Equals(constraintName, StringComparison.OrdinalIgnoreCase));
                    if (constDef != null)
                        constDef.Description = description;

                    var checkDef = entity.CheckConstraints
                        .FirstOrDefault(c => c.Name.Equals(constraintName, StringComparison.OrdinalIgnoreCase));
                    if (checkDef != null)
                        checkDef.Description = description;
                }
            }
        }



        private List<string> ExtractReferencedColumns(string expression, EntityDefinition entity)
        {
            var referenced = new List<string>();
            if (string.IsNullOrWhiteSpace(expression))
                return referenced;

            // نجيب أسماء الأعمدة كلها
            var columnNames = entity.Columns.Select(c => c.Name).ToList();

            // نعمل Normalize للـ Expression
            var normalized = expression
                .Replace("[", "").Replace("]", "") // نشيل الأقواس
                .Replace("(", " ").Replace(")", " ")
                .Replace(">", " ").Replace("<", " ")
                .Replace("=", " ").Replace("!", " ")
                .Replace("+", " ").Replace("-", " ")
                .Replace("*", " ").Replace("/", " ")
                .Replace(",", " ");

            // نعمل Split للكلمات
            var tokens = normalized
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var token in tokens)
            {
                if (columnNames.Any(c => c.Equals(token, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!referenced.Contains(token, StringComparer.OrdinalIgnoreCase))
                        referenced.Add(token);
                }
            }

            return referenced;
        }

        private void AddParam(DbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }
    }
}