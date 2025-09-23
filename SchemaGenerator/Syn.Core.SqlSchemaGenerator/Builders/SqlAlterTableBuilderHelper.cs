using Microsoft.EntityFrameworkCore.Metadata.Internal;

using Syn.Core.Logger;
using Syn.Core.SqlSchemaGenerator.Models;

using System.Text;

namespace Syn.Core.SqlSchemaGenerator.Builders;

public partial class SqlAlterTableBuilder
{
    private string BuildColumnSqlType(ColumnDefinition col)
    {
        // يرجع النوع فقط مع الـ NULL/NOT NULL بدون اسم العمود
        var type = col.TypeName; // مثال: "uniqueidentifier" أو "nvarchar(100)"
        var nullability = col.IsNullable ? "NULL" : "NOT NULL";
        return $"{type} {nullability}";
    }
    private bool NeedsTryConvertToGuid(ColumnDefinition oldCol, ColumnDefinition newCol)
    {
        return oldCol.TypeName.StartsWith("nvarchar", StringComparison.OrdinalIgnoreCase)
            && newCol.TypeName.Equals("uniqueidentifier", StringComparison.OrdinalIgnoreCase);
    }

    private string BuildColumnMigrationScript(
        string schemaName,
        string tableName,
        string columnName,
        string newColumnType,
        string? copyExpression,
        string? defaultExpression,
        bool enforceNotNull,
        EntityDefinition oldEntity,
        EntityDefinition newEntity,
        HashSet<string> droppedConstraints
    )
    {
        var sb = new StringBuilder();
        void Add(string s) => sb.AppendLine(s);

        var qTable = $"[{schemaName}].[{tableName}]";
        var newCol = $"{columnName}_New";

        string nullableType = newColumnType.Contains("NOT NULL", StringComparison.OrdinalIgnoreCase)
            ? newColumnType.Replace("NOT NULL", "NULL", StringComparison.OrdinalIgnoreCase)
            : $"{newColumnType} NULL";

        var copyExpr = !string.IsNullOrWhiteSpace(copyExpression) ? copyExpression : $"[{columnName}]";

        Add($"-- === Safe column migration for {qTable}.{columnName} ===");
        Add("SET NOCOUNT ON;");
        Add("BEGIN TRY");
        Add("    BEGIN TRAN;");

        // 1) إضافة العمود الجديد كـ NULLable
        Add($"    IF COL_LENGTH(N'{schemaName}.{tableName}', N'{newCol}') IS NULL");
        Add($"        ALTER TABLE {qTable} ADD [{newCol}] {nullableType};");

        // 2) نسخ البيانات
        Add("    DECLARE @sql NVARCHAR(MAX);");
        Add($"    SET @sql = N'UPDATE {qTable} SET [{newCol}] = {copyExpr};';");
        Add("    EXEC sp_executesql @sql;");

        // 3) إسقاط DEFAULTs
        Add($@"
    DECLARE cur_dc CURSOR FAST_FORWARD FOR
        SELECT 'ALTER TABLE {qTable} DROP CONSTRAINT [' + dc.name + ']'
        FROM sys.default_constraints dc
        JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'{qTable}') AND c.name = N'{columnName}';
    OPEN cur_dc; FETCH NEXT FROM cur_dc INTO @sql;
    WHILE @@FETCH_STATUS = 0 
    BEGIN 
        -- SAFE DROP
        EXEC(@sql);  
        FETCH NEXT FROM cur_dc INTO @sql;  
    END
    CLOSE cur_dc; DEALLOCATE cur_dc;");

        // تسجيل أسماء DEFAULT constraints
        foreach (var def in oldEntity.Constraints.Where(d => d.Columns.Contains(columnName, StringComparer.OrdinalIgnoreCase)))
            droppedConstraints?.Add(def.Name);

        // 4) إسقاط CHECKs
        Add($@"
    DECLARE cur_ck CURSOR FAST_FORWARD FOR
        SELECT 'ALTER TABLE {qTable} DROP CONSTRAINT [' + cc.name + ']'
        FROM sys.check_constraints cc
        JOIN sys.columns c ON cc.parent_object_id = c.object_id AND cc.parent_column_id = c.column_id
        WHERE cc.parent_object_id = OBJECT_ID(N'{qTable}') AND c.name = N'{columnName}';
    OPEN cur_ck; FETCH NEXT FROM cur_ck INTO @sql;
    WHILE @@FETCH_STATUS = 0 
    BEGIN 
        -- SAFE DROP
        EXEC(@sql);  
        FETCH NEXT FROM cur_ck INTO @sql;  
    END
    CLOSE cur_ck; DEALLOCATE cur_ck;");

        // تسجيل أسماء CHECK constraints
        foreach (var chk in oldEntity.CheckConstraints.Where(c => c.ReferencedColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase)))
            droppedConstraints?.Add(chk.Name);

        // 5) حذف العمود القديم
        Add("    -- SAFE DROP");
        Add($"    ALTER TABLE {qTable} DROP COLUMN [{columnName}];");

        // 6) إعادة تسمية العمود الجديد
        Add($"    EXEC sp_rename N'{schemaName}.{tableName}.{newCol}', N'{columnName}', 'COLUMN';");

        // 7) فرض NOT NULL لو مطلوب
        if (enforceNotNull)
        {
            Add($"    ALTER TABLE {qTable} ALTER COLUMN [{columnName}] {newColumnType};");
        }

        // 8) إعادة إضافة أي قيود CHECK ناقصة
        var relatedChecks = oldEntity.CheckConstraints
            .Where(c => c.ReferencedColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase))
            .ToList();

        foreach (var check in relatedChecks)
        {
            var existsInNew = newEntity.CheckConstraints.Any(c =>
                c.Name.Equals(check.Name, StringComparison.OrdinalIgnoreCase));

            if (!existsInNew)
            {
                ConsoleLog.Success(
                    $"[Safety] Re‑adding CHECK constraint {check.Name} for column '{columnName}'",
                    customPrefix: "ColumnMigration"
                );
                Add($"    ALTER TABLE {qTable} ADD CONSTRAINT [{check.Name}] CHECK ({check.Expression});");
            }
        }

        Add("    COMMIT TRAN;");
        Add("END TRY");
        Add("BEGIN CATCH");
        Add("    IF XACT_STATE() <> 0 ROLLBACK TRAN;");
        Add("    DECLARE @msg NVARCHAR(4000) = ERROR_MESSAGE();");
        Add("    RAISERROR('Safe column migration failed: %s', 16, 1, @msg);");
        Add("END CATCH;");
        Add($"-- === End safe column migration for {qTable}.{columnName} ===");

        return sb.ToString();
    }




    private void AppendAllConstraintChanges(
        StringBuilder sb,
        EntityDefinition oldEntity,
        EntityDefinition newEntity,
        List<string> excludedColumns,
        HashSet<string> droppedConstraints,
        List<string> migratedPkColumns
    )
    {

        // 🆕 دمج الـ ForeignKeys في Constraints لو مش موجودة
        foreach (var fk in newEntity.ForeignKeys)
        {
            var constraintName = string.IsNullOrWhiteSpace(fk.ConstraintName)
                ? $"FK_{newEntity.Name}_{fk.Column}"
                : fk.ConstraintName;

            if (!newEntity.Constraints.Any(c =>
                c.Name.Equals(constraintName, StringComparison.OrdinalIgnoreCase)))
            {
                newEntity.Constraints.Add(new ConstraintDefinition
                {
                    Name = constraintName,
                    Type = "FOREIGN KEY",
                    Columns = new List<string> { fk.Column },
                    ReferencedTable = fk.ReferencedTable,
                    ReferencedSchema = fk.ReferencedSchema,
                    ReferencedColumns = new List<string>
                    {
                        string.IsNullOrWhiteSpace(fk.ReferencedColumn) ? "Id" : fk.ReferencedColumn
                    },
                    OnDelete = fk.OnDelete,
                    OnUpdate = fk.OnUpdate
                });

                ConsoleLog.Info(
                    $"[FK-Merge] Added FK constraint '{constraintName}' from ForeignKeys to Constraints",
                    customPrefix: "ConstraintMigration"
                );
            }
        }

        var newCols = newEntity.NewColumns ?? new List<string>();

        // ===== الفهارس =====
        var processedOldIndexes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var processedNewIndexes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var oldIdx in oldEntity.Indexes)
        {
            var key = $"{oldIdx.Name}|{string.Join(",", oldIdx.Columns)}";
            if (!processedOldIndexes.Add(key))
                continue;

            var match = newEntity.Indexes.FirstOrDefault(i => i.Name.Equals(oldIdx.Name, StringComparison.OrdinalIgnoreCase));
            var changeReasons = GetIndexChangeReasons(oldIdx, match);

            if (match == null || changeReasons.Count > 0)
            {
                if (excludedColumns != null && oldIdx.Columns.Any(cn => excludedColumns.Contains(cn, StringComparer.OrdinalIgnoreCase)))
                    continue;

                if (droppedConstraints != null && droppedConstraints.Contains(oldIdx.Name))
                    continue;

                var dropComment = $"Dropping index: {oldIdx.Name}" + (changeReasons.Count > 0 ? $" ({string.Join(", ", changeReasons)})" : "");
                var dropSql = $@"
IF EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE name = N'{oldIdx.Name}' 
      AND object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    DROP INDEX [{oldIdx.Name}] ON [{newEntity.Schema}].[{newEntity.Name}];";

                // إضافة الوسم لو مش موجود
                if (!dropSql.Contains("-- SAFE DROP", StringComparison.OrdinalIgnoreCase))
                    dropSql = dropSql.Replace("DROP INDEX", "-- SAFE DROP\n    DROP INDEX");

                sb.AppendLine($"-- ❌ {dropComment}");
                sb.AppendLine(dropSql);
                sb.AppendLine("GO");

                droppedConstraints?.Add(oldIdx.Name);
            }
        }

        foreach (var newIdx in newEntity.Indexes)
        {
            var key = $"{newIdx.Name}|{string.Join(",", newIdx.Columns)}";
            if (!processedNewIndexes.Add(key))
                continue;

            var match = oldEntity.Indexes.FirstOrDefault(i => i.Name.Equals(newIdx.Name, StringComparison.OrdinalIgnoreCase));
            var changeReasons = GetIndexChangeReasons(match, newIdx);

            if (changeReasons.Count == 0)
                continue;

            if (match != null && !droppedConstraints.Contains(match.Name))
            {
                var dropComment = $"Dropping index: {match.Name} ({string.Join(", ", changeReasons)})";
                var dropSql = $@"
IF EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE name = N'{match.Name}' 
      AND object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    DROP INDEX [{match.Name}] ON [{newEntity.Schema}].[{newEntity.Name}];";

                // إضافة الوسم لو مش موجود
                if (!dropSql.Contains("-- SAFE DROP", StringComparison.OrdinalIgnoreCase))
                    dropSql = dropSql.Replace("DROP INDEX", "-- SAFE DROP\n    DROP INDEX");

                sb.AppendLine($"-- ❌ {dropComment}");
                sb.AppendLine(dropSql);
                sb.AppendLine("GO");

                droppedConstraints?.Add(match.Name);
            }

            var cols = string.Join(", ", newIdx.Columns.Select(c => $"[{c}]"));
            var unique = newIdx.IsUnique ? "UNIQUE " : "";

            var createComment = $"Creating index: {newIdx.Name} ({string.Join(", ", changeReasons)})";
            var createSql = $@"
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE name = N'{newIdx.Name}' 
      AND object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    CREATE {unique}INDEX [{newIdx.Name}] ON [{newEntity.Schema}].[{newEntity.Name}] ({cols});";

            sb.AppendLine($"-- 🆕 {createComment}");
            sb.AppendLine(createSql);
            sb.AppendLine("GO");
        }

        // =========================
        // ثانياً: القيود (Constraints) PK/FK/Unique Constraints
        // =========================
        var processedOldConstraints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var processedNewConstraints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var oldConst in oldEntity.Constraints)
        {
            if (!processedOldConstraints.Add(oldConst.Name))
                continue;

            var match = newEntity.Constraints.FirstOrDefault(c => c.Name == oldConst.Name);
            var changeReasons = GetConstraintChangeReasons(oldConst, match);

            if (changeReasons.Count > 0)
            {
                if ((excludedColumns != null && oldConst.Columns.Any(cn => excludedColumns.Contains(cn, StringComparer.OrdinalIgnoreCase))) ||
                    (migratedPkColumns != null && oldConst.Columns.Any(cn => migratedPkColumns.Contains(cn, StringComparer.OrdinalIgnoreCase))))
                {
                    var skipMsg = $"Skipped dropping {oldConst.Type} {oldConst.Name} (related to PK migration column)";
                    sb.AppendLine($"-- ⏭️ {skipMsg}");
                    ConsoleLog.Info(skipMsg, customPrefix: "ConstraintMigration");
                    continue;
                }

                if (oldConst.Type.Equals("PRIMARY KEY", StringComparison.OrdinalIgnoreCase) &&
                    !CanAlterPrimaryKey(oldEntity, newEntity))
                {
                    var msg = $"Skipped dropping PRIMARY KEY {oldConst.Name} due to safety check";
                    sb.AppendLine($"-- ⚠️ {msg}");
                    ConsoleLog.Warning(msg, customPrefix: "ConstraintMigration");
                    continue;
                }

                var dropComment = $"Dropping {oldConst.Type}: {oldConst.Name} ({string.Join(", ", changeReasons)})";
                var dropSql = $@"
IF EXISTS (
    SELECT 1 FROM sys.objects 
    WHERE name = N'{oldConst.Name}' 
      AND parent_object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] DROP CONSTRAINT [{oldConst.Name}];";

                // إضافة الوسم لو مش موجود
                if (!dropSql.Contains("-- SAFE DROP", StringComparison.OrdinalIgnoreCase))
                    dropSql = dropSql.Replace("ALTER TABLE", "-- SAFE DROP\n    ALTER TABLE");

                sb.AppendLine($"-- ❌ {dropComment}");
                sb.AppendLine(dropSql);
                sb.AppendLine("GO");

                ConsoleLog.Warning(dropComment, customPrefix: "ConstraintMigration");
                droppedConstraints?.Add(oldConst.Name);
            }
        }

        foreach (var newConst in newEntity.Constraints)
        {
            if (!processedNewConstraints.Add(newConst.Name))
                continue;

            var match = oldEntity.Constraints.FirstOrDefault(c => c.Name == newConst.Name);
            var changeReasons = GetConstraintChangeReasons(match, newConst);

            if (changeReasons.Count > 0)
            {
                var oldColsSet = new HashSet<string>(
                    oldEntity.Columns.Select(c => c.Name?.Trim() ?? string.Empty)
                                     .Where(n => !string.IsNullOrWhiteSpace(n)),
                    StringComparer.OrdinalIgnoreCase);

                bool referencesNewColumn =
                    (newConst.Columns != null && newConst.Columns.Count > 0) &&
                    newConst.Columns
                        .Where(col => !string.IsNullOrWhiteSpace(col))
                        .Any(colName => !oldColsSet.Contains(colName.Trim()));

                bool referencesExcludedColumn = excludedColumns != null &&
                    newConst.Columns.Any(cn => excludedColumns.Contains(cn, StringComparer.OrdinalIgnoreCase));

                bool referencesMigratedPk = migratedPkColumns != null &&
                    newConst.Columns.Any(cn => migratedPkColumns.Contains(cn, StringComparer.OrdinalIgnoreCase));

                if (referencesNewColumn || referencesExcludedColumn || referencesMigratedPk)
                {
                    var msg = $"Skipped adding {newConst.Type} {newConst.Name} (references new or PK-migrated column)";
                    sb.AppendLine($"-- ⏭️ {msg}");
                    ConsoleLog.Info(msg, customPrefix: "ConstraintMigration");
                    continue;
                }

                if (newConst.Type.Equals("PRIMARY KEY", StringComparison.OrdinalIgnoreCase) &&
                    !CanAlterPrimaryKey(oldEntity, newEntity))
                {
                    var msg = $"Skipped adding PRIMARY KEY {newConst.Name} due to safety check";
                    sb.AppendLine($"-- ⚠️ {msg}");
                    ConsoleLog.Warning(msg, customPrefix: "ConstraintMigration");
                    continue;
                }

                var addComment = $"Creating {newConst.Type}: {newConst.Name} ({string.Join(", ", changeReasons)})";
                var addSql = $@"
IF NOT EXISTS (
    SELECT 1 FROM sys.objects 
    WHERE name = N'{newConst.Name}' 
      AND parent_object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    {BuildAddConstraintSql(newEntity, newConst)}";

                sb.AppendLine($"-- 🆕 {addComment}");
                sb.AppendLine(addSql);
                sb.AppendLine("GO");

                ConsoleLog.Success(addComment, customPrefix: "ConstraintMigration");

            }
            else
            {
                var msg = $"Skipped creating {newConst.Type} {newConst.Name} (no changes detected)";
                sb.AppendLine($"-- ⏭️ {msg}");
                ConsoleLog.Info(msg, customPrefix: "ConstraintMigration");
            }
        }

        AppendCheckConstraintChanges(sb, oldEntity, newEntity, excludedColumns, droppedConstraints, migratedPkColumns);
    }


    #region === Check Constraints ===
    private void AppendCheckConstraintChanges(
        StringBuilder sb,
        EntityDefinition oldEntity,
        EntityDefinition newEntity,
        List<string> excludedColumns,
        HashSet<string> droppedConstraints,
        List<string> migratedPkColumns
    )
    {
        var newCols = newEntity.NewColumns ?? new List<string>();

        var processedOldChecks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var processedNewChecks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 🗑️ أولاً: حذف الـ CHECK constraints القديمة أو المعدلة
        foreach (var oldCheck in oldEntity.CheckConstraints)
        {
            if (!processedOldChecks.Add(oldCheck.Name))
                continue;

            if (droppedConstraints != null && droppedConstraints.Contains(oldCheck.Name))
            {
                var skipMsg = $"Skipped dropping CHECK {oldCheck.Name} (already dropped in safe migration)";
                sb.AppendLine($"-- ⏭️ {skipMsg}");
                ConsoleLog.Info(skipMsg, customPrefix: "CheckConstraintMigration");
                continue;
            }

            var match = newEntity.CheckConstraints.FirstOrDefault(c => c.Name == oldCheck.Name);
            var changeReasons = GetCheckConstraintChangeReasons(oldCheck, match);

            if (changeReasons.Count > 0)
            {
                if ((excludedColumns != null && oldCheck.ReferencedColumns.Any(cn => excludedColumns.Contains(cn, StringComparer.OrdinalIgnoreCase))) ||
                    (migratedPkColumns != null && oldCheck.ReferencedColumns.Any(cn => migratedPkColumns.Contains(cn, StringComparer.OrdinalIgnoreCase))))
                {
                    var skipMsg = $"Skipped dropping CHECK {oldCheck.Name} (related to PK migration column)";
                    sb.AppendLine($"-- ⏭️ {skipMsg}");
                    ConsoleLog.Info(skipMsg, customPrefix: "CheckConstraintMigration");
                    continue;
                }

                var dropComment = $"Dropping CHECK: {oldCheck.Name} ({string.Join(", ", changeReasons)})";
                var dropSql = $@"
IF EXISTS (
    SELECT 1 FROM sys.check_constraints cc
    WHERE cc.name = N'{oldCheck.Name}'
      AND cc.parent_object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] DROP CONSTRAINT [{oldCheck.Name}];";

                sb.AppendLine($"-- ❌ {dropComment}");
                sb.AppendLine(dropSql);
                sb.AppendLine("GO");

                ConsoleLog.Warning(dropComment, customPrefix: "CheckConstraintMigration");
                droppedConstraints?.Add(oldCheck.Name);
            }
        }

        // 🆕 ثانياً: إضافة الـ CHECK constraints الجديدة أو المعدلة
        foreach (var newCheck in newEntity.CheckConstraints)
        {
            if (!processedNewChecks.Add(newCheck.Name))
                continue;

            // 🚫 منع التكرار لو القيد موجود بالفعل في Constraints العامة
            if (newEntity.Constraints.Any(c => c.Name.Equals(newCheck.Name, StringComparison.OrdinalIgnoreCase)))
                continue;

            var match = oldEntity.CheckConstraints.FirstOrDefault(c => c.Name == newCheck.Name);
            var changeReasons = GetCheckConstraintChangeReasons(match, newCheck);

            if (changeReasons.Count > 0)
            {
                var oldColsSet = new HashSet<string>(
                    oldEntity.Columns.Select(c => c.Name?.Trim() ?? string.Empty)
                                     .Where(n => !string.IsNullOrWhiteSpace(n)),
                    StringComparer.OrdinalIgnoreCase);

                bool referencesNewColumn =
                    (newCheck.ReferencedColumns != null && newCheck.ReferencedColumns.Count > 0) &&
                    newCheck.ReferencedColumns
                        .Where(col => !string.IsNullOrWhiteSpace(col))
                        .Any(colName => !oldColsSet.Contains(colName.Trim()));

                bool referencesExcludedColumn = excludedColumns != null &&
                    newCheck.ReferencedColumns.Any(cn => excludedColumns.Contains(cn, StringComparer.OrdinalIgnoreCase));

                bool referencesMigratedPk = migratedPkColumns != null &&
                    newCheck.ReferencedColumns.Any(cn => migratedPkColumns.Contains(cn, StringComparer.OrdinalIgnoreCase));

                if (referencesNewColumn || referencesExcludedColumn || referencesMigratedPk)
                {
                    var msg = $"Skipped adding CHECK {newCheck.Name} (references new or PK-migrated column)";
                    sb.AppendLine($"-- ⏭️ {msg}");
                    ConsoleLog.Info(msg, customPrefix: "CheckConstraintMigration");
                    continue;
                }

                var addComment = $"Creating CHECK: {newCheck.Name} ({string.Join(", ", changeReasons)})";
                var addSql = $@"
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints cc
    WHERE cc.name = N'{newCheck.Name}'
      AND cc.parent_object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] ADD CONSTRAINT [{newCheck.Name}] CHECK ({newCheck.Expression});";

                sb.AppendLine($"-- 🆕 {addComment}");
                sb.AppendLine(addSql);
                sb.AppendLine("GO");
                ConsoleLog.Success(addComment, customPrefix: "CheckConstraintMigration");

                // ✅ إضافة وصف للقيد لو موجود وبشرط وجوده فعليًا
                if (!string.IsNullOrWhiteSpace(newCheck.Description))
                {
                    var descSql = $@"
IF EXISTS (
    SELECT 1 FROM sys.check_constraints 
    WHERE name = N'{newCheck.Name}' 
      AND parent_object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
AND NOT EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE name = N'MS_Description'
      AND major_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
      AND minor_id = 0
      AND class = 1
)
EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'{newCheck.Description}', 
    @level0type = N'SCHEMA', @level0name = '{newEntity.Schema}',
    @level1type = N'TABLE',  @level1name = '{newEntity.Name}',
    @level2type = N'CONSTRAINT', @level2name = '{newCheck.Name}';";

                    sb.AppendLine(descSql);
                    sb.AppendLine("GO");

                    ConsoleLog.Info($"Added MS_Description for CHECK {newCheck.Name}", customPrefix: "CheckConstraintMigration");
                }
            }
            else
            {
                var msg = $"Skipped creating CHECK {newCheck.Name} (no changes detected)";
                sb.AppendLine($"-- ⏭️ {msg}");
                ConsoleLog.Info(msg, customPrefix: "CheckConstraintMigration");
            }
        }
    }

    /// <summary>
    /// استخراج أسباب التغيير بين CHECK constraints
    /// </summary>
    private List<string> GetCheckConstraintChangeReasons(CheckConstraintDefinition? oldCheck, CheckConstraintDefinition? newCheck)
    {
        var reasons = new List<string>();

        if (oldCheck == null && newCheck != null)
        {
            reasons.Add("new check constraint");
            return reasons;
        }

        if (oldCheck != null && newCheck == null)
        {
            reasons.Add("check constraint removed");
            return reasons;
        }

        if (oldCheck == null || newCheck == null)
            return reasons;

        if (!string.Equals(Normalize(oldCheck.Expression), Normalize(newCheck.Expression), StringComparison.OrdinalIgnoreCase))
            reasons.Add("expression changed");

        if (!oldCheck.ReferencedColumns.SequenceEqual(newCheck.ReferencedColumns, StringComparer.OrdinalIgnoreCase))
            reasons.Add("referenced columns changed");

        return reasons;
    }

    private string Normalize(string input) =>
        input?.Trim().Replace("(", "").Replace(")", "").Replace(" ", "") ?? string.Empty;
    #endregion


    /// <summary>
    /// استخراج أسباب التغيير بين فهرسين
    /// </summary>
    private List<string> GetIndexChangeReasons(IndexDefinition? oldIdx, IndexDefinition? newIdx)
    {
        var reasons = new List<string>();

        if (oldIdx == null && newIdx != null)
        {
            reasons.Add("new index");
            return reasons;
        }

        if (oldIdx != null && newIdx == null)
        {
            reasons.Add("index removed");
            return reasons;
        }

        if (oldIdx == null || newIdx == null)
            return reasons;

        if (oldIdx.IsUnique != newIdx.IsUnique)
            reasons.Add($"Unique changed {oldIdx.IsUnique} → {newIdx.IsUnique}");

        if (!oldIdx.Columns.SequenceEqual(newIdx.Columns, StringComparer.OrdinalIgnoreCase))
            reasons.Add("columns changed");

        if (!string.Equals(oldIdx.FilterExpression ?? "", newIdx.FilterExpression ?? "", StringComparison.OrdinalIgnoreCase))
            reasons.Add("filter expression changed");

        if (!(oldIdx.IncludeColumns ?? new List<string>())
            .SequenceEqual(newIdx.IncludeColumns ?? new List<string>(), StringComparer.OrdinalIgnoreCase))
            reasons.Add("include columns changed");

        return reasons;
    }


    /// <summary>
    /// استخراج أسباب التغيير بين أي نوع من القيود
    /// </summary>
    private List<string> GetConstraintChangeReasons(ConstraintDefinition? oldConst, ConstraintDefinition? newConst)
    {
        var reasons = new List<string>();

        if (oldConst == null && newConst != null)
        {
            reasons.Add("new constraint");
            return reasons;
        }

        if (oldConst != null && newConst == null)
        {
            reasons.Add("constraint removed");
            return reasons;
        }

        if (oldConst == null || newConst == null)
            return reasons;

        if (!string.Equals(oldConst.Type, newConst.Type, StringComparison.OrdinalIgnoreCase))
            reasons.Add($"type changed {oldConst.Type} → {newConst.Type}");

        if (!oldConst.Columns.SequenceEqual(newConst.Columns, StringComparer.OrdinalIgnoreCase))
            reasons.Add("columns changed");

        if (!string.Equals(oldConst.ReferencedTable ?? "", newConst.ReferencedTable ?? "", StringComparison.OrdinalIgnoreCase))
            reasons.Add("referenced table changed");

        if (!oldConst.ReferencedColumns.SequenceEqual(newConst.ReferencedColumns, StringComparer.OrdinalIgnoreCase))
            reasons.Add("referenced columns changed");

        if (!string.Equals(oldConst.DefaultValue ?? "", newConst.DefaultValue ?? "", StringComparison.OrdinalIgnoreCase))
            reasons.Add("default value changed");

        return reasons;
    }




}
