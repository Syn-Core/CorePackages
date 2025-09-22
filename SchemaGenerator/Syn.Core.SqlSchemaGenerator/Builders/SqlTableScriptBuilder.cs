using Syn.Core.SqlSchemaGenerator.Models;
using Syn.Core.SqlSchemaGenerator.Helper;
using System.Text;

namespace Syn.Core.SqlSchemaGenerator.Builders
{
    /// <summary>
    /// Builds SQL CREATE TABLE scripts based on an EntityDefinition model.
    /// Delegates all metadata creation to <see cref="EntityDefinitionBuilder"/>,
    /// ensuring consistency and single source of truth for schema details.
    /// </summary>
    public class SqlTableScriptBuilder
    {
        private readonly EntityDefinitionBuilder _entityDefinitionBuilder;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTableScriptBuilder"/> class.
        /// </summary>
        /// <param name="entityDefinitionBuilder">The unified metadata builder for entities.</param>
        public SqlTableScriptBuilder(EntityDefinitionBuilder entityDefinitionBuilder)
        {
            _entityDefinitionBuilder = entityDefinitionBuilder
                ?? throw new ArgumentNullException(nameof(entityDefinitionBuilder));
        }

        /// <summary>
        /// Generates a CREATE TABLE SQL script from a CLR type.
        /// </summary>
        public string Build(Type entityType)
        {
            if (entityType == null) throw new ArgumentNullException(nameof(entityType));
            var entityDefinition = _entityDefinitionBuilder.BuildAllWithRelationships(new[] { entityType }).First();
            return Build(entityDefinition);
        }

        /// <summary>
        /// Generates a CREATE TABLE SQL script from an EntityDefinition.
        /// </summary>
        public string Build(EntityDefinition entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var schema = string.IsNullOrWhiteSpace(entity.Schema) ? "dbo" : entity.Schema;
            var sb = new StringBuilder();
            sb.AppendLine($"CREATE TABLE [{schema}].[{entity.Name}] (");

            var columnLines = entity.Columns
                .Select(BuildColumnDefinition)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            if (entity.PrimaryKey != null && entity.PrimaryKey.Columns.Any())
            {
                var pkCols = string.Join(", ", entity.PrimaryKey.Columns.Select(c => $"[{c}]"));
                columnLines.Add($"CONSTRAINT [{entity.PrimaryKey.Name}] PRIMARY KEY ({pkCols})");
            }

            sb.AppendLine("    " + string.Join(",\n    ", columnLines));
            sb.AppendLine(");");

            // ✅ إضافة أوامر الـ FKs بعد إنشاء الجدول
            foreach (var fk in entity.ForeignKeys)
            {
                var constraintName = string.IsNullOrWhiteSpace(fk.ConstraintName)
                    ? $"FK_{entity.Name}_{fk.Column}"
                    : fk.ConstraintName;

                var refColumn = string.IsNullOrWhiteSpace(fk.ReferencedColumn)
                    ? "Id"
                    : fk.ReferencedColumn;

                sb.AppendLine(BuildForeignKeys(entity, schema));
            }

            return sb.ToString();
        }

        private string BuildForeignKeys(EntityDefinition entity, string schema)
        {
            var sb = new StringBuilder();

            foreach (var fk in entity.ForeignKeys)
            {
                var constraintName = string.IsNullOrWhiteSpace(fk.ConstraintName)
                    ? $"FK_{entity.Name}_{fk.Column}"
                    : fk.ConstraintName;

                var refColumn = string.IsNullOrWhiteSpace(fk.ReferencedColumn)
                    ? "Id"
                    : fk.ReferencedColumn;

                // 🆕 استخدم الـ ReferencedSchema لو موجود، وإلا خليه "dbo"
                var refSchema = string.IsNullOrWhiteSpace(fk.ReferencedSchema)
                    ? "dbo"
                    : fk.ReferencedSchema;

                sb.AppendLine($@"
ALTER TABLE [{schema}].[{entity.Name}]
ADD CONSTRAINT [{constraintName}] FOREIGN KEY ([{fk.Column}])
REFERENCES [{refSchema}].[{fk.ReferencedTable}] ([{refColumn}])
ON DELETE {fk.OnDelete.ToSql()}
ON UPDATE {fk.OnUpdate.ToSql()};");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Builds SQL column definition from a <see cref="ColumnDefinition"/>.
        /// Skips navigation properties and ensures correct formatting.
        /// </summary>
        /// <summary>
        /// Builds SQL column definition from a ColumnDefinition.
        /// Ignores navigation properties and ensures consistent formatting.
        /// </summary>
        private string BuildColumnDefinition(ColumnDefinition col)
        {
            if (col.IsNavigationProperty)
                return null;

            Console.WriteLine($"[TRACE:ColumnDef] Building column: {col.Name} → Type={col.TypeName}, Nullable={col.IsNullable}, Identity={col.IsIdentity}");

            var sb = new StringBuilder();

            // 🔹 توحيد كتابة النوع
            var typeName = (col.TypeName ?? "").Trim().ToUpperInvariant();
            sb.Append($"[{col.Name}] {typeName}");

            if (col.IsIdentity)
                sb.Append(" IDENTITY(1,1)");

            if (!col.IsNullable)
                sb.Append(" NOT NULL");

            if (col.DefaultValue != null)
                sb.Append($" DEFAULT {FormatDefaultValue(col.DefaultValue)}");

            return sb.ToString();
        }

        private string FormatDefaultValue(object value) =>
            value switch
            {
                string s => $"'{s}'",
                bool b => b ? "1" : "0",
                _ => value.ToString()
            };
    }
}