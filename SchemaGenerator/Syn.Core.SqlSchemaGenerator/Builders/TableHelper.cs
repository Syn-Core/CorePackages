using Syn.Core.Logger;
using Syn.Core.SqlSchemaGenerator.Helper;
using Syn.Core.SqlSchemaGenerator.Models;

using System.Data;
using System.Text;

namespace Syn.Core.SqlSchemaGenerator.Builders;

internal static class TableHelper
{

    internal static string BuildAddConstraintSql(EntityDefinition entity, ConstraintDefinition constraint)
    {
        var cols = string.Join(", ", constraint.Columns.Select(c => $"[{c}]"));
        var schema = string.IsNullOrWhiteSpace(entity.Schema) ? "dbo" : entity.Schema;

        // فحص خاص بالـ PRIMARY KEY
        if (constraint.Type.Equals("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
        {

            return $"ALTER TABLE [{schema}].[{entity.Name}] ADD CONSTRAINT [{constraint.Name}] PRIMARY KEY ({cols});";
        }

        // فحص خاص بالـ UNIQUE
        if (constraint.Type.Equals("UNIQUE", StringComparison.OrdinalIgnoreCase))
        {
            return $"ALTER TABLE [{schema}].[{entity.Name}] ADD CONSTRAINT [{constraint.Name}] UNIQUE ({cols});";
        }

        // ✅ دعم FOREIGN KEY مع ON DELETE / ON UPDATE
        if (constraint.Type.Equals("FOREIGN KEY", StringComparison.OrdinalIgnoreCase))
        {
            var refColumn = constraint.ReferencedColumns.FirstOrDefault() ?? "Id";
            var onDelete = constraint.OnDelete != ReferentialAction.NoAction ? $" ON DELETE {constraint.OnDelete.ToSql()}" : "";
            var onUpdate = constraint.OnUpdate != ReferentialAction.NoAction ? $" ON UPDATE {constraint.OnUpdate.ToSql()}" : "";

            return $@"
ALTER TABLE [{schema}].[{entity.Name}]
ADD CONSTRAINT [{constraint.Name}] FOREIGN KEY ({cols})
REFERENCES [{constraint.ReferencedSchema}].[{constraint.ReferencedTable}] ([{refColumn}]){onDelete}{onUpdate};";
        }

        // أنواع قيود غير مدعومة
        return $"-- Unsupported constraint type: {constraint.Type} for [{constraint.Name}]";
    }


    internal static void BatchAppendDescriptions(StringBuilder sb, EntityDefinition entity)
    {
        AppendDescriptionForTable(sb, entity);
        AppendDescriptionForColumn(sb, entity);
        AppendDescriptionForConstraints(sb, entity);
    }

    internal static void AppendDescriptionForTable(StringBuilder sb, EntityDefinition entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Description))
            return;

        var schema = string.IsNullOrWhiteSpace(entity.Schema) ? "dbo" : entity.Schema;
        var safeDescription = entity.Description.Replace("'", "''");

        var descSql = $@"
IF EXISTS (
    SELECT 1 
    FROM sys.extended_properties ep
    JOIN sys.objects o ON ep.major_id = o.object_id
    WHERE ep.name = N'MS_Description'
      AND o.object_id = OBJECT_ID(N'{schema}.{entity.Name}')
      AND ep.class = 1
      AND ep.minor_id = 0
)
BEGIN
    EXEC sp_updateextendedproperty 
        @name = N'MS_Description', 
        @value = N'{safeDescription}', 
        @level0type = N'SCHEMA', @level0name = '{schema}',
        @level1type = N'TABLE',  @level1name = '{entity.Name}';
END
ELSE
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'MS_Description', 
        @value = N'{safeDescription}', 
        @level0type = N'SCHEMA', @level0name = '{schema}',
        @level1type = N'TABLE',  @level1name = '{entity.Name}';
END";

        sb.AppendLine($"-- 📝 Adding table description for [{schema}].[{entity.Name}]");
        sb.AppendLine(descSql);
        sb.AppendLine("GO");

        ConsoleLog.Info($"Add/Update MS_Description for table [{schema}].[{entity.Name}]", customPrefix: "TableDescription");
    }

    internal static void AppendDescriptionForColumn(StringBuilder sb, EntityDefinition entity)
    {
        var schema = string.IsNullOrWhiteSpace(entity.Schema) ? "dbo" : entity.Schema;

        foreach (var col in entity.Columns)
        {
            // وصف العمود الأساسي
            var fullDescription = col.Description ?? string.Empty;

            // أوصاف كل القيود المرتبطة بالعمود
            var constraintDescriptions = GetConstraintDescriptionsForColumn(entity, col.Name);
            if (!string.IsNullOrWhiteSpace(constraintDescriptions))
            {
                if (!string.IsNullOrWhiteSpace(fullDescription))
                    fullDescription += Environment.NewLine;
                fullDescription += constraintDescriptions;
            }

            if (string.IsNullOrWhiteSpace(fullDescription))
                continue;

            var safeDescription = fullDescription.Replace("'", "''");

            var descSql = $@"
IF EXISTS (
    SELECT 1 
    FROM sys.extended_properties ep
    JOIN sys.columns c ON ep.major_id = c.object_id AND ep.minor_id = c.column_id
    WHERE ep.name = N'MS_Description'
      AND c.object_id = OBJECT_ID(N'{schema}.{entity.Name}')
      AND c.name = '{col.Name}'
)
BEGIN
    EXEC sp_updateextendedproperty 
        @name = N'MS_Description', 
        @value = N'{safeDescription}', 
        @level0type = N'SCHEMA', @level0name = '{schema}',
        @level1type = N'TABLE',  @level1name = '{entity.Name}',
        @level2type = N'COLUMN', @level2name = '{col.Name}';
END
ELSE
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'MS_Description', 
        @value = N'{safeDescription}', 
        @level0type = N'SCHEMA', @level0name = '{schema}',
        @level1type = N'TABLE',  @level1name = '{entity.Name}',
        @level2type = N'COLUMN', @level2name = '{col.Name}';
END";

            sb.AppendLine($"-- 📝 Adding description for column [{col.Name}]");
            sb.AppendLine(descSql);
            sb.AppendLine("GO");

            ConsoleLog.Info($"Add/Update MS_Description for column [{col.Name}]", customPrefix: "ColumnDescription");
        }
    }

    internal static void AppendDescriptionForConstraints(StringBuilder sb, EntityDefinition entity)
    {
        var schema = string.IsNullOrWhiteSpace(entity.Schema) ? "dbo" : entity.Schema;

        foreach (var constraint in entity.Constraints)
        {
            if (string.IsNullOrWhiteSpace(constraint.Description))
                continue;

            // 🟢 PK / FK / UNIQUE / DEFAULT / CHECK
            if (!(constraint.Type.Equals("PRIMARY KEY", StringComparison.OrdinalIgnoreCase) ||
                constraint.Type.Equals("FOREIGN KEY", StringComparison.OrdinalIgnoreCase) ||
                constraint.Type.Equals("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
                constraint.Type.Equals("DEFAULT", StringComparison.OrdinalIgnoreCase) ||
                constraint.Type.Equals("CHECK", StringComparison.OrdinalIgnoreCase)))
                continue;

            var sql = BuildConstraintDescriptionSql(schema, entity.Name, constraint.Name, constraint.Type, constraint.Description);
            sb.AppendLine(sql);
            sb.AppendLine("GO");

            ConsoleLog.Info($"Added/Updated MS_Description for {constraint.Type} {constraint.Name}", customPrefix: "CheckConstraintMigration");
        }
    }


    // النسخة العامة: لكل الأنواع
    internal static void AppendDescriptionForConstraint(
        StringBuilder sb,
        string schema,
        string tableName,
        ConstraintDefinition constraint
    )
    {
        if (string.IsNullOrWhiteSpace(constraint.Description))
            return;

        // 🟢 PK / FK / UNIQUE / DEFAULT / CHECK
        if (!(constraint.Type.Equals("PRIMARY KEY", StringComparison.OrdinalIgnoreCase) ||
              constraint.Type.Equals("FOREIGN KEY", StringComparison.OrdinalIgnoreCase) ||
              constraint.Type.Equals("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
              constraint.Type.Equals("DEFAULT", StringComparison.OrdinalIgnoreCase) ||
              constraint.Type.Equals("CHECK", StringComparison.OrdinalIgnoreCase)))
            return;

        var sql = BuildConstraintDescriptionSql(schema, tableName, constraint.Name, constraint.Type, constraint.Description);
        sb.AppendLine(sql);
        sb.AppendLine("GO");

        ConsoleLog.Info($"Added/Updated MS_Description for {constraint.Type} {constraint.Name}", customPrefix: "ConstraintMigration");
    }

    // النسخة الخاصة: للـ CHECK فقط
    internal static void AppendDescriptionForConstraint(
        StringBuilder sb,
        string schema,
        string tableName,
        CheckConstraintDefinition checkConstraint
    )
    {
        if (string.IsNullOrWhiteSpace(checkConstraint.Description))
            return;
        var sql = BuildConstraintDescriptionSql(schema, tableName, checkConstraint.Name, "CHECK", checkConstraint.Description);
        sb.AppendLine(sql);
        sb.AppendLine("GO");

        ConsoleLog.Info($"Added/Updated MS_Description for CHECK {checkConstraint.Name}", customPrefix: "CheckConstraintMigration");
    }

    private static string BuildConstraintDescriptionSql(
    string schema,
    string tableName,
    string constraintName,
    string constraintType,
    string description
)
    {
        schema = string.IsNullOrWhiteSpace(schema) ? "dbo" : schema;
        var safeDescription = description.Replace("'", "''");

        return $@"
-- 📝 Add/Update description for {constraintType} constraint [{constraintName}]
IF NOT EXISTS (
    SELECT 1
    FROM sys.fn_listextendedproperty (
        N'MS_Description',
        'SCHEMA', N'{schema}',
        'TABLE',  N'{tableName}',
        'CONSTRAINT', N'{constraintName}'
    )
)
BEGIN
    EXEC sys.sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'{safeDescription}',
        @level0type = N'SCHEMA', @level0name = '{schema}',
        @level1type = N'TABLE',  @level1name = '{tableName}',
        @level2type = N'CONSTRAINT', @level2name = '{constraintName}';
END
ELSE
BEGIN
    EXEC sys.sp_updateextendedproperty
        @name = N'MS_Description',
        @value = N'{safeDescription}',
        @level0type = N'SCHEMA', @level0name = '{schema}',
        @level1type = N'TABLE',  @level1name = '{tableName}',
        @level2type = N'CONSTRAINT', @level2name = '{constraintName}';
END";
    }


    private static string GetConstraintDescriptionsForColumn(EntityDefinition entity, string columnName)
    {
        var relatedConstraints = entity.Constraints
            .Where(c => c.ReferencedColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (!relatedConstraints.Any())
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var constraint in relatedConstraints)
        {
            if (!string.IsNullOrWhiteSpace(constraint.Description))
            {
                sb.AppendLine($"- {constraint.Type} {constraint.Name}: {constraint.Description}");
            }
            else
            {
                sb.AppendLine($"- {constraint.Type} {constraint.Name}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    internal static void AppendSafeDropConstraintSql(
        StringBuilder sb,
        string schema,
        string table,
        string constraintName,
        ExcuteQuery queryHelper)
    {
        ConsoleLog.Info($"[SafeDrop] Checking constraint [{constraintName}] on [{schema}].[{table}]");

        bool isEmpty = queryHelper.IsTableEmpty(schema, table);

        if (isEmpty)
        {
            sb.AppendLine($"-- SAFE DROP: {constraintName} (table empty)");
            sb.AppendLine($"ALTER TABLE [{schema}].[{table}] DROP CONSTRAINT [{constraintName}];");
            ConsoleLog.Success($"[SafeDrop] Constraint {constraintName} dropped safely (table empty).");
        }
        else
        {
            sb.AppendLine($"-- Skipped DROP of {constraintName} (table has data)");
            ConsoleLog.Warning($"[SafeDrop] Constraint {constraintName} skipped (table not empty).");
        }

        sb.AppendLine("GO");
    }

}
