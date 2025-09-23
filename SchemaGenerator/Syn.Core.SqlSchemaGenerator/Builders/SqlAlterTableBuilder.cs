using Microsoft.Data.SqlClient;

using Syn.Core.Logger;
using Syn.Core.SqlSchemaGenerator.Helper;
using Syn.Core.SqlSchemaGenerator.Models;

using System.Text;

namespace Syn.Core.SqlSchemaGenerator.Builders;

/// <summary>
/// Generates ALTER TABLE SQL scripts to migrate an existing table definition
/// to match a target definition. Supports columns, indexes, PK/FK/Unique constraints,
/// and Check Constraints.
/// </summary>
public partial class SqlAlterTableBuilder
{
    private readonly SqlTableScriptBuilder _tableScriptBuilder;
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance using an <see cref="EntityDefinitionBuilder"/> for schema extraction.
    /// </summary>
    public SqlAlterTableBuilder(EntityDefinitionBuilder entityDefinitionBuilder, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentNullException(nameof(connectionString));
        _tableScriptBuilder = new SqlTableScriptBuilder(entityDefinitionBuilder);
        _connectionString = connectionString;
    }

    /// <summary>
    /// Builds ALTER TABLE SQL script comparing two <see cref="EntityDefinition"/> objects.
    /// </summary>
    public string Build(EntityDefinition oldEntity, EntityDefinition newEntity)
    {
        if (oldEntity == null) throw new ArgumentNullException(nameof(oldEntity));
        if (newEntity == null) throw new ArgumentNullException(nameof(newEntity));

        if (oldEntity.Columns.Count == 0 && oldEntity.Constraints.Count == 0)
        {
            ConsoleLog.Info("===== Final Migration Script =====", customPrefix: "Build");
            var createScript = _tableScriptBuilder.Build(newEntity);
            ConsoleLog.Info(createScript, customPrefix: "Build");
            ConsoleLog.Info("===== End of Script =====", customPrefix: "Build");
            return createScript;
        }

        var pkBuilder = new StringBuilder();
        var sbAddColumns = new StringBuilder();
        var sbOtherChanges = new StringBuilder();

        var droppedConstraints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 🆕 Get migrated PK columns
        var migratedPkColumns = MigratePrimaryKeyIfTypeChanged(pkBuilder, oldEntity, newEntity, droppedConstraints);

        // 1️⃣ أعمدة جديدة
        AppendColumnChanges(sbAddColumns, oldEntity, newEntity, migratedPkColumns, droppedConstraints);

        // 2️⃣ باقي التغييرات (Indexes + Constraints)
        AppendAllConstraintChanges(sbOtherChanges, oldEntity, newEntity, migratedPkColumns, droppedConstraints, migratedPkColumns);


        // 3️⃣ دمج السكريبت النهائي
        var finalScript = new StringBuilder();

        if (pkBuilder.Length > 0)
        {
            var safe = IsBatchSafe(pkBuilder.ToString());
            finalScript.AppendLine(safe
                ? "\n-- SAFE DROP ===== Batch 1: Migrate PrimaryKey If Type Changed ====="
                : "\n-- ===== Batch 1: Migrate PrimaryKey If Type Changed =====");
            finalScript.Append(pkBuilder);
            finalScript.AppendLine();
        }

        if (sbAddColumns.Length > 0)
        {
            var safe = IsBatchSafe(sbAddColumns.ToString());
            finalScript.AppendLine(safe
                ? "\n-- SAFE DROP ===== Batch 2: Add new columns ====="
                : "\n-- ===== Batch 2: Add new columns =====");
            finalScript.Append(sbAddColumns);
            finalScript.AppendLine();
        }

        if (sbOtherChanges.Length > 0)
        {
            var safe = IsBatchSafe(sbOtherChanges.ToString());
            finalScript.AppendLine(safe
                ? "\n-- SAFE DROP ===== Batch 3: Other changes ====="
                : "\n-- ===== Batch 3: Other changes =====");
            finalScript.Append(sbOtherChanges);
            finalScript.AppendLine();
        }

        //TableHelper.AppendDescriptionForTable(finalScript, newEntity);

        //TableHelper.AppendDescriptionForColumn(finalScript, newEntity);

        ConsoleLog.Info("===== Final Migration Script =====", customPrefix: "Build");
        ConsoleLog.Info(finalScript.ToString(), customPrefix: "Build");
        ConsoleLog.Info("===== End of Script =====", customPrefix: "Build");

        return finalScript.ToString();
    }


    //public string Build(EntityDefinition oldEntity, EntityDefinition newEntity)
    //{
    //    if (oldEntity == null) throw new ArgumentNullException(nameof(oldEntity));
    //    if (newEntity == null) throw new ArgumentNullException(nameof(newEntity));

    //    if (oldEntity.Columns.Count == 0 && oldEntity.Constraints.Count == 0)
    //    {
    //        ConsoleLog.Info("===== Final Migration Script =====", customPrefix: "Build");
    //        var createScript = _tableScriptBuilder.Build(newEntity);
    //        ConsoleLog.Info(createScript, customPrefix: "Build");
    //        ConsoleLog.Info("===== End of Script =====", customPrefix: "Build");
    //        return createScript;
    //    }

    //    var pkBuilder = new StringBuilder();
    //    var sbAddColumns = new StringBuilder();
    //    var sbOtherChanges = new StringBuilder();

    //    var droppedConstraints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    //    // 🆕 Get migrated PK columns
    //    var migratedPkColumns = MigratePrimaryKeyIfTypeChanged(pkBuilder, oldEntity, newEntity, droppedConstraints);

    //    // 1️⃣ أعمدة جديدة
    //    AppendColumnChanges(sbAddColumns, oldEntity, newEntity, migratedPkColumns, droppedConstraints);

    //    // 2️⃣ باقي التغييرات (Indexes + Constraints مع بعض)
    //    AppendAllConstraintChanges(sbOtherChanges, oldEntity, newEntity, migratedPkColumns, droppedConstraints, migratedPkColumns);

    //    // 3️⃣ دمج السكريبت
    //    var finalScript = new StringBuilder();

    //    if (pkBuilder.Length > 0)
    //    {
    //        var safe = IsBatchSafe(pkBuilder.ToString());
    //        finalScript.AppendLine(safe
    //            ? "\n-- SAFE DROP ===== Batch 1: Migrate PrimaryKey If Type Changed ====="
    //            : "\n-- ===== Batch 1: Migrate PrimaryKey If Type Changed =====");
    //        finalScript.Append(pkBuilder);
    //        finalScript.AppendLine();
    //    }
    //    if (sbAddColumns.Length > 0)
    //    {
    //        var safe = IsBatchSafe(sbAddColumns.ToString());
    //        finalScript.AppendLine(safe
    //            ? "\n-- SAFE DROP ===== Batch 2: Add new columns ====="
    //            : "\n-- ===== Batch 2: Add new columns =====");
    //        finalScript.Append(sbAddColumns);
    //        finalScript.AppendLine();
    //    }
    //    if (sbOtherChanges.Length > 0)
    //    {
    //        var safe = IsBatchSafe(sbOtherChanges.ToString());
    //        finalScript.AppendLine(safe
    //            ? "\n-- SAFE DROP ===== Batch 3: Other changes ====="
    //            : "\n-- ===== Batch 3: Other changes =====");
    //        finalScript.Append(sbOtherChanges);
    //        finalScript.AppendLine();
    //    }

    //    ConsoleLog.Info("===== Final Migration Script =====", customPrefix: "Build");
    //    ConsoleLog.Info(finalScript.ToString(), customPrefix: "Build");
    //    ConsoleLog.Info("===== End of Script =====", customPrefix: "Build");

    //    string _final = finalScript.ToString();
    //    return _final;
    //}

    bool IsBatchSafe(string batchSql)
    {
        // أي DROP CONSTRAINT أو DROP INDEX لازم يكون قبله -- SAFE DROP
        var lines = batchSql.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if ((line.StartsWith("ALTER TABLE", StringComparison.OrdinalIgnoreCase) && line.Contains("DROP CONSTRAINT", StringComparison.OrdinalIgnoreCase)) ||
                (line.StartsWith("DROP INDEX", StringComparison.OrdinalIgnoreCase)))
            {
                // لازم يكون السطر اللي قبله فيه الوسم
                if (i == 0 || !lines[i - 1].Contains("-- SAFE DROP", StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }
        return true;
    }



    //public string Build(EntityDefinition oldEntity, EntityDefinition newEntity)
    //{
    //    if (oldEntity == null) throw new ArgumentNullException(nameof(oldEntity));
    //    if (newEntity == null) throw new ArgumentNullException(nameof(newEntity));

    //    if (oldEntity.Columns.Count == 0 && oldEntity.Constraints.Count == 0)
    //    {
    //        ConsoleLog.Info("===== Final Migration Script =====", customPrefix: "Build");
    //        var createScript = _tableScriptBuilder.Build(newEntity);
    //        ConsoleLog.Info(createScript, customPrefix: "Build");
    //        ConsoleLog.Info("===== End of Script =====", customPrefix: "Build");
    //        return createScript;
    //    }

    //    var pkBuilder = new StringBuilder();
    //    var sbAddColumns = new StringBuilder();
    //    var sbOtherChanges = new StringBuilder();

    //    var droppedConstraints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    //    // 🆕 Get migrated PK columns
    //    var migratedPkColumns = MigratePrimaryKeyIfTypeChanged(pkBuilder, oldEntity, newEntity, droppedConstraints);

    //    // 1️⃣ أعمدة جديدة
    //    AppendColumnChanges(sbAddColumns, oldEntity, newEntity, migratedPkColumns, droppedConstraints);

    //    // 2️⃣ باقي التغييرات
    //    AppendConstraintChanges(sbOtherChanges, oldEntity, newEntity, migratedPkColumns, droppedConstraints);
    //    AppendCheckConstraintChanges(sbOtherChanges, oldEntity, newEntity, migratedPkColumns, droppedConstraints, migratedPkColumns);
    //    AppendIndexChanges(sbOtherChanges, oldEntity, newEntity, migratedPkColumns, droppedConstraints);
    //    AppendForeignKeyChanges(sbOtherChanges, oldEntity, newEntity, migratedPkColumns, droppedConstraints);

    //    // 3️⃣ دمج السكريبت
    //    var finalScript = new StringBuilder();

    //    if (pkBuilder.Length > 0)
    //    {
    //        finalScript.AppendLine("\n-- ===== Batch 1: Migrate PrimaryKey If Type Changed =====");
    //        finalScript.Append(pkBuilder);
    //        finalScript.AppendLine();
    //    }
    //    if (sbAddColumns.Length > 0)
    //    {
    //        finalScript.AppendLine("\n-- ===== Batch 2: Add new columns =====");
    //        finalScript.Append(sbAddColumns);
    //        finalScript.AppendLine();
    //    }
    //    if (sbOtherChanges.Length > 0)
    //    {
    //        finalScript.AppendLine("\n-- ===== Batch 3: Other changes =====");
    //        finalScript.Append(sbOtherChanges);
    //    }

    //    ConsoleLog.Info("===== Final Migration Script =====", customPrefix: "Build");
    //    ConsoleLog.Info(finalScript.ToString(), customPrefix: "Build");
    //    ConsoleLog.Info("===== End of Script =====", customPrefix: "Build");

    //    return finalScript.ToString();
    //}


    //public string Build(EntityDefinition oldEntity, EntityDefinition newEntity)
    //{
    //    if (oldEntity == null) throw new ArgumentNullException(nameof(oldEntity));
    //    if (newEntity == null) throw new ArgumentNullException(nameof(newEntity));

    //    // 🆕 If table doesn't exist, generate CREATE TABLE script
    //    if (oldEntity.Columns.Count == 0 && oldEntity.Constraints.Count == 0)
    //    {
    //        return _tableScriptBuilder.Build(newEntity);
    //    }

    //    // 🔧 Otherwise, generate ALTER TABLE script
    //    var sb = new StringBuilder();

    //    AppendColumnChanges(sb, oldEntity, newEntity);
    //    AppendConstraintChanges(sb, oldEntity, newEntity);
    //    AppendCheckConstraintChanges(sb, oldEntity, newEntity);
    //    AppendIndexChanges(sb, oldEntity, newEntity);
    //    AppendForeignKeyChanges(sb, oldEntity, newEntity);
    //    var r = sb.ToString();
    //    return r;
    //}

    #region === Columns ===

    private void AppendColumnChanges(
        StringBuilder sb,
        EntityDefinition oldEntity,
        EntityDefinition newEntity,
        List<string> excludedColumns,
        HashSet<string> droppedConstraints
    )
    {
        if (newEntity.NewColumns == null)
            newEntity.NewColumns = new List<string>();

        var processedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var newCol in newEntity.Columns)
        {
            if (excludedColumns != null && excludedColumns.Contains(newCol.Name, StringComparer.OrdinalIgnoreCase))
            {
                var skipComment = $"-- ⏭️ Skipped column: {newCol.Name} (handled in PK migration)";
                sb.AppendLine(skipComment);
                continue;
            }

            if (!processedColumns.Add(newCol.Name))
                continue;

            var oldCol = oldEntity.Columns
                .FirstOrDefault(c => c.Name.Equals(newCol.Name, StringComparison.OrdinalIgnoreCase));

            if (oldCol == null)
            {
                var comment = $"-- 🆕 Adding column: {newCol.Name}";
                var sql = $@"
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE Name = N'{newCol.Name}' 
      AND Object_ID = Object_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] ADD {BuildColumnDefinition(newCol)};";

                sb.AppendLine(comment);
                sb.AppendLine(sql);
                sb.AppendLine("GO");

                newEntity.NewColumns.Add(newCol.Name);
            }
            else if (!ColumnsAreEquivalent(oldCol, newCol))
            {
                if (CanAlterColumn(oldCol, newCol, newEntity.Schema, newEntity.Name))
                {
                    var comment = $"-- 🔧 Altering column: {newCol.Name}";
                    var sql = BuildAlterColumn(oldCol, newCol, newEntity.Name, newEntity.Schema, newEntity, excludedColumns);

                    // إضافة الوسم لو فيه DROP
                    if (sql.Contains("DROP CONSTRAINT", StringComparison.OrdinalIgnoreCase) && !sql.Contains("-- SAFE DROP"))
                        sql = sql.Replace("ALTER TABLE", "-- SAFE DROP\nALTER TABLE");
                    if (sql.Contains("DROP INDEX", StringComparison.OrdinalIgnoreCase) && !sql.Contains("-- SAFE DROP"))
                        sql = sql.Replace("DROP INDEX", "-- SAFE DROP\nDROP INDEX");

                    sb.AppendLine(comment);
                    sb.AppendLine(sql);
                    sb.AppendLine("GO");
                }
                else
                {
                    var comment = $"-- 🔄 Safe-migrating column: {newCol.Name}";
                    sb.AppendLine(comment);

                    var sqlType = BuildColumnSqlType(newCol);
                    string? copyExpr = NeedsTryConvertToGuid(oldCol, newCol)
                        ? $"COALESCE(TRY_CONVERT(uniqueidentifier, [{newCol.Name}]), NEWID())"
                        : null;

                    string? defaultExpr = null;

                    var safeScript = BuildColumnMigrationScript(
                        newEntity.Schema ?? "dbo",
                        newEntity.Name,
                        newCol.Name,
                        sqlType,
                        copyExpression: copyExpr,
                        defaultExpression: defaultExpr,
                        enforceNotNull: sqlType.Contains("NOT NULL", StringComparison.OrdinalIgnoreCase),
                        oldEntity,
                        newEntity,
                        droppedConstraints
                    );

                    var colSuffix = newCol.Name.Replace(" ", "_");
                    safeScript = safeScript
                        .Replace("@CheckName", $"@CheckName_{colSuffix}")
                        .Replace("check_cursor", $"check_cursor_{colSuffix}")
                        .Replace("@IndexName", $"@IndexName_{colSuffix}")
                        .Replace("idx_cursor", $"idx_cursor_{colSuffix}");

                    // إضافة الوسم لو فيه DROP
                    if (safeScript.Contains("DROP CONSTRAINT", StringComparison.OrdinalIgnoreCase) && !safeScript.Contains("-- SAFE DROP"))
                        safeScript = safeScript.Replace("ALTER TABLE", "-- SAFE DROP\nALTER TABLE");
                    if (safeScript.Contains("DROP INDEX", StringComparison.OrdinalIgnoreCase) && !safeScript.Contains("-- SAFE DROP"))
                        safeScript = safeScript.Replace("DROP INDEX", "-- SAFE DROP\nDROP INDEX");

                    sb.AppendLine(safeScript);
                    sb.AppendLine("GO");
                }
            }
        }

        foreach (var oldCol in oldEntity.Columns)
        {
            if (excludedColumns != null && excludedColumns.Contains(oldCol.Name, StringComparer.OrdinalIgnoreCase))
                continue;

            if (!processedColumns.Add(oldCol.Name))
                continue;

            if (!newEntity.Columns.Any(c => c.Name.Equals(oldCol.Name, StringComparison.OrdinalIgnoreCase)))
            {
                var comment = $"-- ❌ Dropping column: {oldCol.Name}";
                var sql = $@"
IF EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE Name = N'{oldCol.Name}' 
      AND Object_ID = Object_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    -- SAFE DROP
    ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] DROP COLUMN [{oldCol.Name}];";

                sb.AppendLine(comment);
                sb.AppendLine(sql);
                sb.AppendLine("GO");
            }
        }
    }




    /// <summary>
    /// Compares two column definitions to determine if they are equivalent.
    /// Now considers text length differences (e.g., nvarchar(max) → nvarchar(600)) as non-equivalent.
    /// </summary>
    private bool ColumnsAreEquivalent(ColumnDefinition oldCol, ColumnDefinition newCol)
    {
        string baseOldType = oldCol.TypeName.Split('(')[0].Trim().ToLowerInvariant();
        string baseNewType = newCol.TypeName.Split('(')[0].Trim().ToLowerInvariant();

        // النوع الأساسي لازم يكون نفسه
        if (!string.Equals(baseOldType, baseNewType, StringComparison.OrdinalIgnoreCase))
            return false;

        // نفس الـ Identity
        if (oldCol.IsIdentity != newCol.IsIdentity)
            return false;

        // نفس الـ Nullable
        if (oldCol.IsNullable != newCol.IsNullable)
            return false;

        // نفس الـ DefaultValue
        if (!string.Equals(oldCol.DefaultValue?.ToString()?.Trim(),
                           newCol.DefaultValue?.ToString()?.Trim(),
                           StringComparison.OrdinalIgnoreCase))
            return false;

        // 🆕 فحص فرق الطول لو النوع نصي
        if (IsTextType(baseOldType))
        {
            int oldLen = ExtractLengthForIndex(oldCol.TypeName); // -1 = max
            int newLen = ExtractLengthForIndex(newCol.TypeName);

            // لو الطول مختلف (بما في ذلك max → رقم أو رقم → max) نعتبرهم غير متساويين
            if (oldLen != newLen)
                return false;
        }

        // لو النوع رقمي، ممكن تضيف فحص Precision/Scale لو حابب
        return true;
    }



    /// <summary>
    /// Builds an ALTER COLUMN SQL statement with safety checks and smart length adjustments.
    /// </summary>

    private string BuildAlterColumn(
        ColumnDefinition oldCol,
        ColumnDefinition newCol,
        string tableName,
        string schema,
        EntityDefinition newEntity,
        List<string> migratedPkColumns)
    {
        // 🛡️ Skip if PK migration already handled this column
        if (migratedPkColumns != null &&
            migratedPkColumns.Contains(newCol.Name, StringComparer.OrdinalIgnoreCase))
        {
            return $"-- Skipped ALTER COLUMN for {newCol.Name} because PK migration already handled it";
        }

        // 🛡️ فحص الأمان لتغيير Identity
        if (oldCol.IsIdentity != newCol.IsIdentity && !IsTableEmpty(schema, tableName))
        {
            WarnOnce($"{schema}.{tableName}.{newCol.Name}.Identity",
                $"⚠️ [ALTER] Skipped {schema}.{tableName}.{newCol.Name} → Identity change unsafe (table has data)");
            return $"-- Skipped ALTER COLUMN for {newCol.Name} due to Identity change on non-empty table";
        }

        // 🛡️ فحص الأمان لتغيير NOT NULL
        if (!newCol.IsNullable && oldCol.IsNullable)
        {
            int nullCount = ColumnNullCount(schema, tableName, newCol.Name);
            if (nullCount > 0)
            {
                WarnOnce($"{schema}.{tableName}.{newCol.Name}.NotNull",
                    $"⚠️ [ALTER] Skipped {schema}.{tableName}.{newCol.Name} → NOT NULL change unsafe (NULL values exist)");
                return $"-- Skipped ALTER COLUMN for {newCol.Name} due to NULL values in column";
            }
        }

        // 🛠️ أمر ALTER COLUMN
        var nullable = newCol.IsNullable ? "NULL" : "NOT NULL";
        var alterColumn = $@"
ALTER TABLE [{schema}].[{tableName}]
ALTER COLUMN [{newCol.Name}] {newCol.TypeName} {nullable};";

        // 🛠️ إسقاط الـ CHECK constraints المرتبطة بالعمود
        var dropChecks = $@"
-- Drop dependent CHECK constraints for column {newCol.Name}
DECLARE @CheckName SYSNAME;
DECLARE check_cursor CURSOR FOR
SELECT cc.name
FROM sys.check_constraints cc
JOIN sys.columns col ON cc.parent_object_id = col.object_id
    AND cc.parent_column_id = col.column_id
JOIN sys.tables t ON col.object_id = t.object_id
JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE t.name = '{tableName}'
  AND s.name = '{schema}'
  AND col.name = '{newCol.Name}';

OPEN check_cursor;
FETCH NEXT FROM check_cursor INTO @CheckName;
WHILE @@FETCH_STATUS = 0
BEGIN
    PRINT 'Dropping CHECK constraint: ' + @CheckName;
    -- SAFE DROP
    EXEC('ALTER TABLE [{schema}].[{tableName}] DROP CONSTRAINT [' + @CheckName + ']');
    FETCH NEXT FROM check_cursor INTO @CheckName;
END
CLOSE check_cursor;
DEALLOCATE check_cursor;
";

        // 🛠️ إسقاط الـ Indexes المرتبطة بالعمود
        var dropIndexes = $@"
-- Drop dependent indexes for column {newCol.Name}
DECLARE @IndexName SYSNAME;
DECLARE idx_cursor CURSOR FOR
SELECT i.name
FROM sys.indexes i
JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
JOIN sys.tables t ON c.object_id = t.object_id
JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE t.name = '{tableName}'
  AND s.name = '{schema}'
  AND c.name = '{newCol.Name}'
  AND i.is_primary_key = 0
  AND i.is_unique_constraint = 0;

OPEN idx_cursor;
FETCH NEXT FROM idx_cursor INTO @IndexName;
WHILE @@FETCH_STATUS = 0
BEGIN
    PRINT 'Dropping index: ' + @IndexName;
    -- SAFE DROP
    EXEC('DROP INDEX [' + @IndexName + '] ON [{schema}].[{tableName}]');
    FETCH NEXT FROM idx_cursor INTO @IndexName;
END
CLOSE idx_cursor;
DEALLOCATE idx_cursor;
";

        // 🛠️ إرجاع السكريبت النهائي
        return dropChecks + "\n" + dropIndexes + "\n" + alterColumn;
    }



    // Runner
    private static readonly HashSet<string> _warnedKeys = new(StringComparer.OrdinalIgnoreCase);

    private bool WarnOnce(string key, string message)
    {
        if (_warnedKeys.Contains(key))
        {
            HelperMethod._suppressedWarnings.Add($"[{key}]");
            return false;
        }

        Console.WriteLine(message);
        _warnedKeys.Add(key);
        return true;
    }



    /// <summary>
    /// Checks if the SQL type is a text-based type.
    /// </summary>
    private bool IsTextType(string typeName) =>
        typeName.StartsWith("nvarchar", StringComparison.OrdinalIgnoreCase) ||
        typeName.StartsWith("varchar", StringComparison.OrdinalIgnoreCase) ||
        typeName.StartsWith("nchar", StringComparison.OrdinalIgnoreCase) ||
        typeName.StartsWith("char", StringComparison.OrdinalIgnoreCase);



    private string BuildColumnDefinition(ColumnDefinition col)
    {
        var sb = new StringBuilder();
        sb.Append($"[{col.Name}] {col.TypeName}");

        if (!col.IsNullable)
            sb.Append(" NOT NULL");

        if (col.IsIdentity)
            sb.Append(" IDENTITY(1,1)");

        if (col.DefaultValue != null)
            sb.Append($" DEFAULT {HelperMethod.FormatDefaultValue(col.DefaultValue)}");

        return sb.ToString();
    }
    #endregion

    #region === PK/FK/Unique Constraints ===
    private void AppendConstraintChanges(
        StringBuilder sb,
        EntityDefinition oldEntity,
        EntityDefinition newEntity,
        List<string> excludedColumns,
        HashSet<string> droppedConstraints
    )
    {
        var newCols = newEntity.NewColumns ?? new List<string>();

        var processedOldConstraints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var processedNewConstraints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 🗑️ أولاً: حذف القيود القديمة أو المعدلة
        foreach (var oldConst in oldEntity.Constraints)
        {
            if (!processedOldConstraints.Add(oldConst.Name))
                continue;

            if (droppedConstraints != null && droppedConstraints.Contains(oldConst.Name))
            {
                var skipMsg = $"Skipped dropping constraint {oldConst.Name} (already dropped in safe migration)";
                sb.AppendLine($"-- ⏭️ {skipMsg}");
                ConsoleLog.Info(skipMsg, customPrefix: "ConstraintMigration");
                continue;
            }

            var match = newEntity.Constraints.FirstOrDefault(c => c.Name == oldConst.Name);
            var changeReasons = GetConstraintChangeReasons(oldConst, match);

            if (changeReasons.Count > 0)
            {
                if (excludedColumns != null && oldConst.Columns.Any(cn => excludedColumns.Contains(cn, StringComparer.OrdinalIgnoreCase)))
                {
                    var skipMsg = $"Skipped dropping constraint {oldConst.Name} (related to PK migration column)";
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

                var dropComment = $"Dropping constraint: {oldConst.Name} ({string.Join(", ", changeReasons)})";
                var dropSql = $@"
IF EXISTS (
    SELECT 1 FROM sys.objects 
    WHERE name = N'{oldConst.Name}' 
      AND parent_object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] DROP CONSTRAINT [{oldConst.Name}];";

                sb.AppendLine($"-- ❌ {dropComment}");
                sb.AppendLine(dropSql);
                sb.AppendLine("GO");

                ConsoleLog.Warning(dropComment, customPrefix: "ConstraintMigration");
                droppedConstraints?.Add(oldConst.Name);
            }
        }

        // 🆕 ثانياً: إضافة القيود الجديدة أو المعدلة
        foreach (var newConst in newEntity.Constraints)
        {
            if (!processedNewConstraints.Add(newConst.Name))
                continue;

            var match = oldEntity.Constraints.FirstOrDefault(c => c.Name == newConst.Name);
            var changeReasons = GetConstraintChangeReasons(match, newConst);

            if (changeReasons.Count > 0)
            {
                // تجهيز مجموعة بأسماء أعمدة الجدول القديم لسرعة وأمان الفحص
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

                if (referencesNewColumn || referencesExcludedColumn)
                {
                    var msg = $"Skipped adding constraint {newConst.Name} (references new or PK-migrated column)";
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

                var addComment = $"Creating constraint: {newConst.Name} ({string.Join(", ", changeReasons)})";
                var addSql = $@"
IF NOT EXISTS (
    SELECT 1 FROM sys.objects 
    WHERE name = N'{newConst.Name}' 
      AND parent_object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    {BuildAddConstraintSql(newEntity, newConst)}";

                sb.AppendLine($"-- 🆕 {addComment}");
                sb.AppendLine(addSql);

                ConsoleLog.Success(addComment, customPrefix: "ConstraintMigration");
            }
            else
            {
                var msg = $"Skipped creating constraint {newConst.Name} (no changes detected)";
                sb.AppendLine($"-- ⏭️ {msg}");
                ConsoleLog.Info(msg, customPrefix: "ConstraintMigration");
            }
        }
    }


    private string BuildAddConstraintSql(EntityDefinition entity, ConstraintDefinition constraint)
    {
        var cols = string.Join(", ", constraint.Columns.Select(c => $"[{c}]"));
        var schema = string.IsNullOrWhiteSpace(entity.Schema) ? "dbo" : entity.Schema;

        // فحص خاص بالـ PRIMARY KEY
        if (constraint.Type.Equals("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
        {
            // لو الجدول مش فاضي وكان التغيير على الـ Identity بس → نتجنب الإضافة
            if (!IsTableEmpty(entity.Schema, entity.Name))
            {
                Console.WriteLine($"⚠️ Skipped adding PRIMARY KEY [{constraint.Name}] on {entity.Schema}.{entity.Name} because table has data and Identity change is unsafe.");
                return $"-- Skipped adding PRIMARY KEY [{constraint.Name}] due to data safety check";
            }

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



    #endregion


    #region === Indexes ===

    private void AppendIndexChanges(
        StringBuilder sb,
        EntityDefinition oldEntity,
        EntityDefinition newEntity,
        List<string> excludedColumns,
        HashSet<string> droppedConstraints
    )
    {
        ConsoleLog.Info($"[DEBUG] OldEntity.Indexes ({oldEntity.Indexes.Count}):", customPrefix: "IndexMigration");
        foreach (var idx in oldEntity.Indexes)
            ConsoleLog.Info($"    - {idx.Name} | Unique={idx.IsUnique} | Cols=[{string.Join(", ", idx.Columns)}]", customPrefix: "IndexMigration");

        ConsoleLog.Info($"[DEBUG] NewEntity.Indexes ({newEntity.Indexes.Count}):", customPrefix: "IndexMigration");
        foreach (var idx in newEntity.Indexes)
            ConsoleLog.Info($"    - {idx.Name} | Unique={idx.IsUnique} | Cols=[{string.Join(", ", idx.Columns)}]", customPrefix: "IndexMigration");

        var processedOldIndexes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 🗑️ فحص الفهارس المحذوفة
        foreach (var oldIdx in oldEntity.Indexes)
        {
            var key = $"{oldIdx.Name}|{string.Join(",", oldIdx.Columns)}";
            if (!processedOldIndexes.Add(key))
                continue;

            if (droppedConstraints != null && droppedConstraints.Contains(oldIdx.Name))
            {
                var skipMsg = $"Skipped dropping index {oldIdx.Name} (already dropped in safe migration)";
                sb.AppendLine($"-- ⏭️ {skipMsg}");
                ConsoleLog.Info(skipMsg, customPrefix: "IndexMigration");
                continue;
            }

            if (excludedColumns != null && oldIdx.Columns.Any(cn => excludedColumns.Contains(cn, StringComparer.OrdinalIgnoreCase)))
            {
                var skipMsg = $"Skipped dropping index {oldIdx.Name} because it's related to PK migration column";
                sb.AppendLine($"-- ⏭️ {skipMsg}");
                ConsoleLog.Info(skipMsg, customPrefix: "IndexMigration");
                continue;
            }

            if (!newEntity.Indexes.Any(i => i.Name.Equals(oldIdx.Name, StringComparison.OrdinalIgnoreCase) &&
                                            i.Columns.SequenceEqual(oldIdx.Columns, StringComparer.OrdinalIgnoreCase)))
            {
                var dropComment = $"Dropping index: {oldIdx.Name} (index not found in new entity)";
                var dropSql = $@"
IF EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE name = N'{oldIdx.Name}' 
      AND object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    DROP INDEX [{oldIdx.Name}] ON [{newEntity.Schema}].[{newEntity.Name}];";

                sb.AppendLine($"-- ❌ {dropComment}");
                sb.AppendLine(dropSql);

                ConsoleLog.Warning(dropComment, customPrefix: "IndexMigration");
                droppedConstraints?.Add(oldIdx.Name);
            }
        }

        var processedNewIndexes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 🆕 فحص الفهارس الجديدة أو المعدلة
        foreach (var newIdx in newEntity.Indexes)
        {
            var key = $"{newIdx.Name}|{string.Join(",", newIdx.Columns)}";
            if (!processedNewIndexes.Add(key))
                continue;

            if (excludedColumns != null && newIdx.Columns.Any(cn => excludedColumns.Contains(cn, StringComparer.OrdinalIgnoreCase)))
            {
                var skipMsg = $"Skipped creating index {newIdx.Name} because it's related to PK migration column";
                sb.AppendLine($"-- ⏭️ {skipMsg}");
                ConsoleLog.Info(skipMsg, customPrefix: "IndexMigration");
                continue;
            }

            var existingIdx = oldEntity.Indexes.FirstOrDefault(i =>
                i.Name.Equals(newIdx.Name, StringComparison.OrdinalIgnoreCase) &&
                i.Columns.SequenceEqual(newIdx.Columns, StringComparer.OrdinalIgnoreCase));

            // 🆕 تحديد سبب التغيير
            List<string> changeReasons = new();
            if (existingIdx == null) changeReasons.Add("new index");
            else
            {
                if (existingIdx.IsUnique != newIdx.IsUnique)
                    changeReasons.Add($"Unique changed {existingIdx.IsUnique} → {newIdx.IsUnique}");
                if (!string.Equals(existingIdx.FilterExpression ?? "", newIdx.FilterExpression ?? "", StringComparison.OrdinalIgnoreCase))
                    changeReasons.Add("Filter expression changed");
                if (!(existingIdx.IncludeColumns ?? new List<string>())
                    .SequenceEqual(newIdx.IncludeColumns ?? new List<string>(), StringComparer.OrdinalIgnoreCase))
                    changeReasons.Add("Include columns changed");
            }

            bool indexIsNewOrChanged = changeReasons.Count > 0;

            if (!indexIsNewOrChanged)
            {
                var skipMsg = $"Skipped creating index {newIdx.Name} (no changes detected)";
                sb.AppendLine($"-- ⏭️ {skipMsg}");
                ConsoleLog.Info(skipMsg, customPrefix: "IndexMigration");
                continue;
            }

            // 🛠️ Drop/Create للفهرس المعدل أو الجديد
            if (existingIdx != null)
            {
                if (droppedConstraints != null && droppedConstraints.Contains(existingIdx.Name))
                {
                    var skipMsg = $"Skipped dropping index {existingIdx.Name} (already dropped in safe migration)";
                    sb.AppendLine($"-- ⏭️ {skipMsg}");
                    ConsoleLog.Info(skipMsg, customPrefix: "IndexMigration");
                }
                else
                {
                    var dropComment = $"Dropping index: {existingIdx.Name} ({string.Join(", ", changeReasons)})";
                    var dropSql = $@"
IF EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE name = N'{existingIdx.Name}' 
      AND object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    DROP INDEX [{existingIdx.Name}] ON [{newEntity.Schema}].[{newEntity.Name}];";

                    sb.AppendLine($"-- ❌ {dropComment}");
                    sb.AppendLine(dropSql);
                    sb.AppendLine("GO");

                    ConsoleLog.Warning(dropComment, customPrefix: "IndexMigration");
                    droppedConstraints?.Add(existingIdx.Name);
                }
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

            ConsoleLog.Success(createComment, customPrefix: "IndexMigration");
        }
    }




    private List<string> MigratePrimaryKeyIfTypeChanged(
        StringBuilder sb,
        EntityDefinition oldEntity,
        EntityDefinition newEntity,
        HashSet<string> droppedConstraints // 🆕 تمرير الهاش من Build
    )
    {
        var migratedColumns = new List<string>();

        if (newEntity.PrimaryKey == null || !newEntity.PrimaryKey.Columns.Any())
            return migratedColumns;

        var newPkName = newEntity.PrimaryKey.Columns.First();
        var newPkCol = newEntity.Columns
            .FirstOrDefault(c => c.Name.Equals(newPkName, StringComparison.OrdinalIgnoreCase));

        if (newPkCol == null)
            return migratedColumns;

        var oldPkCol = oldEntity.Columns
            .FirstOrDefault(c => c.Name.Equals(newPkName, StringComparison.OrdinalIgnoreCase));

        var oldPkConstraintName = oldEntity.PrimaryKey?.Name;

        bool isPkInOld = oldEntity.PrimaryKey != null &&
                         oldEntity.PrimaryKey.Columns.Any(c => c.Equals(newPkName, StringComparison.OrdinalIgnoreCase));

        bool typeChanged = oldPkCol != null &&
                           !oldPkCol.TypeName.Equals(newPkCol.TypeName, StringComparison.OrdinalIgnoreCase);

        bool identityChanged = oldPkCol != null &&
                               oldPkCol.IsIdentity != newPkCol.IsIdentity;

        // ✅ الحالة 1: PK موجود والنوع/الـ Identity اتغير
        if (isPkInOld && (typeChanged || identityChanged))
        {
            var newColumnType = $"{newPkCol.TypeName} NOT NULL";

            var migrationScript = BuildPkMigrationScript(
                newEntity.Schema ?? "dbo",
                newEntity.Name,
                newPkName,
                oldPkConstraintName,
                newColumnType,
                addPrimaryKeyIfMissing: false,
                oldEntity,
                newEntity,
                droppedConstraints // 🆕 تمرير الهاش
            );

            ConsoleLog.Info($"🆕 PK type changed from {oldPkCol.TypeName} to {newPkCol.TypeName}", customPrefix: "PKMigration");
            sb.AppendLine(migrationScript);

            migratedColumns.Add(newPkName);
        }
        // ✅ الحالة 2: العمود مش PK في القديم، لكنه PK في الجديد
        else if (!isPkInOld)
        {
            var newColumnType = $"{newPkCol.TypeName} NOT NULL";

            var migrationScript = BuildPkMigrationScript(
                newEntity.Schema ?? "dbo",
                newEntity.Name,
                newPkName,
                oldPkConstraintName,
                newColumnType,
                addPrimaryKeyIfMissing: true,
                oldEntity,
                newEntity,
                droppedConstraints // 🆕 تمرير الهاش
            );

            ConsoleLog.Info($"🆕 Promoting column {newPkName} to PRIMARY KEY", customPrefix: "PKMigration");
            sb.AppendLine(migrationScript);

            migratedColumns.Add(newPkName);
        }
        // ✅ الحالة 3: الـ PK لم يتغير → فحص القيود
        else
        {
            ConsoleLog.Info($"[Info] PK '{newPkName}' is unchanged — no migration needed.", customPrefix: "PKMigration");

            var relatedChecks = oldEntity.CheckConstraints
                .Where(c => c.ReferencedColumns.Contains(newPkName, StringComparer.OrdinalIgnoreCase))
                .ToList();

            foreach (var check in relatedChecks)
            {
                var existsInNew = newEntity.CheckConstraints.Any(c =>
                    c.Name.Equals(check.Name, StringComparison.OrdinalIgnoreCase));

                if (!existsInNew)
                {
                    ConsoleLog.Warning(
                        $"[Safety] CHECK constraint {check.Name} is missing in new entity — will be added",
                        customPrefix: "PKMigration"
                    );

                    // إضافة القيد الناقص
                    sb.AppendLine($@"
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints cc
    WHERE cc.name = N'{check.Name}'
      AND cc.parent_object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] ADD CONSTRAINT [{check.Name}] CHECK ({check.Expression});");

                    droppedConstraints?.Add(check.Name); // 🆕 تسجيله
                }
                else
                {
                    ConsoleLog.Info(
                        $"[Safety] CHECK constraint {check.Name} exists in both old and new entities",
                        customPrefix: "PKMigration"
                    );
                }
            }
        }

        return migratedColumns;
    }



    private string BuildPkMigrationScript(
        string schemaName,
        string tableName,
        string columnName,
        string? oldPkConstraintName,
        string newColumnType,
        bool addPrimaryKeyIfMissing,
        EntityDefinition oldEntity,
        EntityDefinition newEntity,
        HashSet<string> droppedConstraints // 🆕 تمرير الهاش
    )
    {
        var sb = new StringBuilder();
        void Add(string s) => sb.AppendLine(s);

        var qTable = $"[{schemaName}].[{tableName}]";
        var newCol = $"{columnName}_New";

        string nullableType = newColumnType.Contains("NOT NULL", StringComparison.OrdinalIgnoreCase)
            ? newColumnType.Replace("NOT NULL", "NULL", StringComparison.OrdinalIgnoreCase)
            : $"{newColumnType} NULL";

        ConsoleLog.Info($"=== Building PK migration script for {qTable}.{columnName} ===", customPrefix: "PKMigration");

        Add($"-- === PK migration for {qTable}.{columnName} ===");
        Add("SET NOCOUNT ON;");
        Add("BEGIN TRY");
        Add("    BEGIN TRAN;");

        // 1) إضافة العمود الجديد كـ NULLable
        Add($"    IF COL_LENGTH(N'{schemaName}.{tableName}', N'{newCol}') IS NULL");
        Add($"        ALTER TABLE {qTable} ADD [{newCol}] {nullableType};");

        // 2) نسخ البيانات
        Add("    DECLARE @sql NVARCHAR(MAX);");
        Add($"    SET @sql = N'UPDATE {qTable} SET [{newCol}] = [{columnName}]';");
        Add("    EXEC sp_executesql @sql;");

        // 3) تحويله إلى NOT NULL
        Add($"    SET @sql = N'ALTER TABLE {qTable} ALTER COLUMN [{newCol}] {newColumnType};';");
        Add("    EXEC sp_executesql @sql;");

        // 4) إسقاط PK القديم
        if (!string.IsNullOrWhiteSpace(oldPkConstraintName))
        {
            Add($"    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'{oldPkConstraintName}' AND parent_object_id = OBJECT_ID(N'{qTable}'))");
            Add($"        ALTER TABLE {qTable} DROP CONSTRAINT [{oldPkConstraintName}];");
            droppedConstraints?.Add(oldPkConstraintName); // 🆕 تسجيله
        }

        // 5) إسقاط DEFAULT/CHECK على العمود القديم
        Add("    DECLARE @dc NVARCHAR(MAX), @ck NVARCHAR(MAX);");

        // Drop DEFAULTs
        Add($@"
    DECLARE cur_dc CURSOR FAST_FORWARD FOR
        SELECT 'ALTER TABLE {qTable} DROP CONSTRAINT [' + dc.name + ']'
        FROM sys.default_constraints dc
        JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'{qTable}') AND c.name = N'{columnName}';
    OPEN cur_dc; FETCH NEXT FROM cur_dc INTO @dc;
    WHILE @@FETCH_STATUS = 0 BEGIN EXEC(@dc); FETCH NEXT FROM cur_dc INTO @dc; END
    CLOSE cur_dc; DEALLOCATE cur_dc;");
        // 🆕 تسجيل كل DEFAULT في droppedConstraints
        foreach (var def in oldEntity.Constraints.Where(d => d.Columns.Contains(columnName, StringComparer.OrdinalIgnoreCase)))
            droppedConstraints?.Add(def.Name);

        // Drop CHECKs
        Add($@"
    DECLARE cur_ck CURSOR FAST_FORWARD FOR
        SELECT 'ALTER TABLE {qTable} DROP CONSTRAINT [' + cc.name + ']'
        FROM sys.check_constraints cc
        JOIN sys.columns c ON cc.parent_object_id = c.object_id AND cc.parent_column_id = c.column_id
        WHERE cc.parent_object_id = OBJECT_ID(N'{qTable}') AND c.name = N'{columnName}';
    OPEN cur_ck; FETCH NEXT FROM cur_ck INTO @ck;
    WHILE @@FETCH_STATUS = 0 BEGIN EXEC(@ck); FETCH NEXT FROM cur_ck INTO @ck; END
    CLOSE cur_ck; DEALLOCATE cur_ck;");
        // 🆕 تسجيل كل CHECK في droppedConstraints
        foreach (var chk in oldEntity.CheckConstraints.Where(c => c.ReferencedColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase)))
            droppedConstraints?.Add(chk.Name);

        // 6) حذف العمود القديم
        Add($"    ALTER TABLE {qTable} DROP COLUMN [{columnName}];");

        // 7) إعادة تسمية العمود الجديد
        Add($"    EXEC sp_rename N'{schemaName}.{tableName}.{newCol}', N'{columnName}', 'COLUMN';");

        // 8) إضافة PK جديد
        if (addPrimaryKeyIfMissing || !string.IsNullOrWhiteSpace(oldPkConstraintName))
        {
            var pkName = oldPkConstraintName ?? $"PK_{tableName}";
            Add($"    ALTER TABLE {qTable} ADD CONSTRAINT [{pkName}] PRIMARY KEY ([{columnName}]);");
        }

        // 9) إعادة إضافة أي قيود CHECK ناقصة
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
                    $"[Safety] Re‑adding CHECK constraint {check.Name} for PK column '{columnName}'",
                    customPrefix: "PKMigration"
                );
                Add($"    ALTER TABLE {qTable} ADD CONSTRAINT [{check.Name}] CHECK ({check.Expression});");
            }
        }

        Add("    COMMIT TRAN;");
        Add("END TRY");
        Add("BEGIN CATCH");
        Add("    IF XACT_STATE() <> 0 ROLLBACK TRAN;");
        Add("    DECLARE @msg NVARCHAR(4000) = ERROR_MESSAGE();");
        Add("    RAISERROR('PK migration failed: %s', 16, 1, @msg);");
        Add("END CATCH;");
        Add($"-- === End PK migration for {qTable}.{columnName} ===");

        return sb.ToString();
    }


    // 🛠️ ميثود مساعدة لحساب حجم العمود بالبايت
    private int GetColumnMaxLength(string typeName)
    {
        // مثال: nvarchar(150) → 150 * 2 بايت
        //       varchar(300)  → 300 * 1 بايت
        //       int           → 4 بايت
        //       decimal(18,2) → 9 بايت تقريبًا

        typeName = typeName.ToLowerInvariant();

        if (typeName.StartsWith("nvarchar"))
            return ExtractLengthForIndex(typeName) * 2;
        if (typeName.StartsWith("varchar"))
            return ExtractLengthForIndex(typeName);
        if (typeName.StartsWith("nchar"))
            return ExtractLengthForIndex(typeName) * 2;
        if (typeName.StartsWith("char"))
            return ExtractLengthForIndex(typeName);

        return typeName switch
        {
            "int" => 4,
            "bigint" => 8,
            "smallint" => 2,
            "tinyint" => 1,
            "bit" => 1,
            _ => 0
        };

    }

    // 🛠️ استخراج الطول من النص (مثال: nvarchar(150) → 150)
    private int ExtractLengthForIndex(string typeName)
    {
        var start = typeName.IndexOf('(');
        var end = typeName.IndexOf(')');
        if (start > 0 && end > start)
        {
            var numStr = typeName.Substring(start + 1, end - start - 1);
            if (int.TryParse(numStr, out int len))
                return len;
        }
        return 0;
    }

    /// <summary>
    /// Checks if an index (single or composite) exceeds SQL Server's 900-byte key size limit.
    /// </summary>
    private bool IsIndexTooLarge(IndexDefinition index, List<ColumnDefinition> allColumns)
    {
        int totalBytes = 0;

        foreach (var colName in index.Columns)
        {
            var col = allColumns.FirstOrDefault(c =>
                c.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));

            if (col != null)
            {
                totalBytes += GetColumnMaxLength(col.TypeName);

                // لو النوع max → نستخدم طول آمن مؤقت للحساب
                if (IsMaxType(col.TypeName))
                    totalBytes += GetColumnMaxLength("nvarchar(450)");
            }
        }

        return totalBytes > 900;
    }

    /// <summary>
    /// Detects if a SQL type is defined as (max).
    /// </summary>
    private bool IsMaxType(string typeName)
    {
        var start = typeName.IndexOf('(');
        var end = typeName.IndexOf(')');
        if (start > 0 && end > start)
        {
            var numStr = typeName.Substring(start + 1, end - start - 1).Trim();
            return numStr.Equals("max", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }





    private int ColumnNullCount(string schema, string tableName, string columnName)
    {
        var sql = $@"SELECT COUNT(*) FROM [{schema}].[{tableName}] WHERE [{columnName}] IS NULL";
        return ExecuteScalar<int>(sql);
    }



    #endregion

    #region === Foreign Keys ===
    private void AppendForeignKeyChanges(
        StringBuilder sb,
        EntityDefinition oldEntity,
        EntityDefinition newEntity,
        List<string> excludedColumns,
        HashSet<string> droppedConstraints
    )
    {
        var newCols = newEntity.NewColumns ?? new List<string>();

        var oldFks = oldEntity.Constraints.Where(c => c.Type == "FOREIGN KEY").ToList();
        var newFks = newEntity.Constraints.Where(c => c.Type == "FOREIGN KEY").ToList();

        var processedOldFks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var processedNewFks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ❌ حذف العلاقات القديمة
        foreach (var oldFk in oldFks)
        {
            if (!processedOldFks.Add(oldFk.Name))
                continue;

            if (droppedConstraints != null && droppedConstraints.Contains(oldFk.Name))
            {
                var skipMsg = $"Skipped dropping FK {oldFk.Name} (already dropped in safe migration)";
                sb.AppendLine($"-- ⏭️ {skipMsg}");
                ConsoleLog.Info(skipMsg, customPrefix: "ForeignKeyMigration");
                continue;
            }

            if (excludedColumns != null && oldFk.Columns.Any(cn => excludedColumns.Contains(cn, StringComparer.OrdinalIgnoreCase)))
            {
                var skipMsg = $"Skipped dropping FK {oldFk.Name} (related to PK migration column)";
                sb.AppendLine($"-- ⏭️ {skipMsg}");
                ConsoleLog.Info(skipMsg, customPrefix: "ForeignKeyMigration");
                continue;
            }

            var match = newFks.FirstOrDefault(f => f.Name.Equals(oldFk.Name, StringComparison.OrdinalIgnoreCase));
            var changeReasons = GetForeignKeyChangeReasons(oldFk, match);

            if (match == null || changeReasons.Count > 0)
            {
                var dropComment = $"Dropping FK: {oldFk.Name}" + (changeReasons.Count > 0 ? $" ({string.Join(", ", changeReasons)})" : "");
                var dropSql = $@"
IF EXISTS (
    SELECT 1 FROM sys.foreign_keys 
    WHERE name = N'{oldFk.Name}' 
      AND parent_object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] DROP CONSTRAINT [{oldFk.Name}];";

                sb.AppendLine($"-- ❌ {dropComment}");
                sb.AppendLine(dropSql);
                sb.AppendLine("GO");

                ConsoleLog.Warning(dropComment, customPrefix: "ForeignKeyMigration");
                droppedConstraints?.Add(oldFk.Name);
            }
        }

        // 🆕 إضافة أو تعديل العلاقات الجديدة
        foreach (var newFk in newFks)
        {
            if (!processedNewFks.Add(newFk.Name))
                continue;

            if (excludedColumns != null && newFk.Columns.Any(cn => excludedColumns.Contains(cn, StringComparer.OrdinalIgnoreCase)))
            {
                var skipMsg = $"Skipped adding FK {newFk.Name} (related to PK migration column)";
                sb.AppendLine($"-- ⏭️ {skipMsg}");
                ConsoleLog.Info(skipMsg, customPrefix: "ForeignKeyMigration");
                continue;
            }

            var match = oldFks.FirstOrDefault(f => f.Name == newFk.Name);
            var changeReasons = GetForeignKeyChangeReasons(match, newFk);

            if (changeReasons.Count == 0)
                continue;

            var oldColsSet = new HashSet<string>(
                oldEntity.Columns.Select(c => c.Name?.Trim() ?? string.Empty)
                                 .Where(n => !string.IsNullOrWhiteSpace(n)),
                StringComparer.OrdinalIgnoreCase);

            bool referencesNewColumn =
                (newFk.Columns != null && newFk.Columns.Count > 0) &&
                newFk.Columns
                    .Where(col => !string.IsNullOrWhiteSpace(col))
                    .Any(colName => !oldColsSet.Contains(colName.Trim()));

            if (referencesNewColumn)
            {
                var msg = $"Skipped adding FK {newFk.Name} (references new column in this migration)";
                sb.AppendLine($"-- ⏭️ {msg}");
                ConsoleLog.Info(msg, customPrefix: "ForeignKeyMigration");
                continue;
            }

            // 🛠️ لو الـ FK موجود لكن مختلف → Drop + GO قبل الـ Add
            if (match != null)
            {
                if (droppedConstraints != null && droppedConstraints.Contains(match.Name))
                {
                    var skipMsg = $"Skipped dropping FK {match.Name} (already dropped in safe migration)";
                    sb.AppendLine($"-- ⏭️ {skipMsg}");
                    ConsoleLog.Info(skipMsg, customPrefix: "ForeignKeyMigration");
                }
                else
                {
                    var dropComment = $"Dropping FK: {match.Name} ({string.Join(", ", changeReasons)})";
                    var dropSql = $@"
IF EXISTS (
    SELECT 1 FROM sys.foreign_keys 
    WHERE name = N'{match.Name}' 
      AND parent_object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] DROP CONSTRAINT [{match.Name}];";

                    sb.AppendLine($"-- ❌ {dropComment}");
                    sb.AppendLine(dropSql);
                    sb.AppendLine("GO");

                    ConsoleLog.Warning(dropComment, customPrefix: "ForeignKeyMigration");
                    droppedConstraints?.Add(match.Name);
                }
            }

            var cols = string.Join(", ", newFk.Columns.Select(c => $"[{c}]"));
            var refCols = string.Join(", ", newFk.ReferencedColumns.Select(c => $"[{c}]"));

            var addComment = $"Creating FK: {newFk.Name} ({string.Join(", ", changeReasons)})";
            var addSql = $@"
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys 
    WHERE name = N'{newFk.Name}' 
      AND parent_object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}]
    ADD CONSTRAINT [{newFk.Name}]
    FOREIGN KEY ({cols})
    REFERENCES [{newEntity.Schema}].[{newFk.ReferencedTable}] ({refCols});";

            sb.AppendLine($"-- 🆕 {addComment}");
            sb.AppendLine(addSql);

            ConsoleLog.Success(addComment, customPrefix: "ForeignKeyMigration");
        }
    }

    /// <summary>
    /// استخراج أسباب التغيير بين FK قديم وجديد
    /// </summary>
    private List<string> GetForeignKeyChangeReasons(ConstraintDefinition? oldFk, ConstraintDefinition? newFk)
    {
        var reasons = new List<string>();

        if (oldFk == null && newFk != null)
        {
            reasons.Add("new foreign key");
            return reasons;
        }

        if (oldFk != null && newFk == null)
        {
            reasons.Add("foreign key removed");
            return reasons;
        }

        if (oldFk == null || newFk == null)
            return reasons;

        if (!string.Equals(oldFk.ReferencedTable ?? "", newFk.ReferencedTable ?? "", StringComparison.OrdinalIgnoreCase))
            reasons.Add("referenced table changed");

        if (!oldFk.Columns.SequenceEqual(newFk.Columns, StringComparer.OrdinalIgnoreCase))
            reasons.Add("columns changed");

        if (!oldFk.ReferencedColumns.SequenceEqual(newFk.ReferencedColumns, StringComparer.OrdinalIgnoreCase))
            reasons.Add("referenced columns changed");

        return reasons;
    }

    /// <summary>
    /// Determines if a column change can be applied using ALTER COLUMN instead of Drop & Add.
    /// </summary>
    private bool CanAlterColumn(ColumnDefinition oldCol, ColumnDefinition newCol, string schema, string tableName)
    {
        if (oldCol == null || newCol == null)
            return false;

        string baseOldType = oldCol.TypeName.Split('(')[0].Trim().ToLowerInvariant();
        string baseNewType = newCol.TypeName.Split('(')[0].Trim().ToLowerInvariant();

        if (!string.Equals(baseOldType, baseNewType, StringComparison.OrdinalIgnoreCase))
            return false;

        // ✅ تعديل الـ Identity فقط لو الجدول فاضي
        if (oldCol.IsIdentity != newCol.IsIdentity)
        {
            if (IsTableEmpty(schema, tableName))
            {
                Console.WriteLine($"[INFO] Table {schema}.{tableName} is empty → allowing Identity change for {newCol.Name}");
                return true;
            }
            else
            {
                Console.WriteLine($"⚠️ Skipped Identity change for {newCol.Name} because table has data.");
                return false;
            }
        }

        // ✅ التحويل إلى NOT NULL فقط لو مفيش NULL
        if (!newCol.IsNullable && oldCol.IsNullable)
        {
            if (ColumnHasNulls(schema, tableName, newCol.Name))
            {
                Console.WriteLine($"⚠️ Skipped NOT NULL change for {newCol.Name} because it contains NULL values.");
                return false;
            }
        }

        bool nullabilityChanged = oldCol.IsNullable != newCol.IsNullable;
        bool lengthChanged = false;

        if (baseOldType.Contains("char"))
        {
            int? oldLen = ExtractLength(oldCol.TypeName);
            int? newLen = ExtractLength(newCol.TypeName);
            if (oldLen != newLen)
                lengthChanged = true;
        }

        if (baseOldType.Contains("decimal") || baseOldType.Contains("numeric"))
        {
            if (oldCol.Precision != newCol.Precision || oldCol.Scale != newCol.Scale)
                lengthChanged = true;
        }

        if (lengthChanged || nullabilityChanged)
            return true;

        return false;
    }
    /// <summary>
    /// Extracts the length from a SQL type name like nvarchar(150).
    /// </summary>
    private int? ExtractLength(string typeName)
    {
        var match = System.Text.RegularExpressions.Regex.Match(typeName, @"\((\d+)\)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int len))
            return len;
        return null;
    }

    /// <summary>
    /// Checks if a table has no rows.
    /// </summary>
    private bool IsTableEmpty(string schema, string tableName)
    {
        var sql = $@"
SELECT COUNT(*) 
FROM [{schema}].[{tableName}]";

        // ✅ استخدام ExecuteScalar<int> بدل الكود اليدوي
        int rowCount = ExecuteScalar<int>(sql);

        // ✅ تتبع واضح
        if (rowCount == 0)
            Console.WriteLine($"[TRACE:TableCheck] {schema}.{tableName} → Table is empty");
        else
            Console.WriteLine($"[TRACE:TableCheck] {schema}.{tableName} → Table has {rowCount} rows");

        return rowCount == 0;
    }


    /// <summary>
    /// Checks if a column contains any NULL values.
    /// </summary>
    private bool ColumnHasNulls(string schema, string tableName, string columnName)
    {
        var sql = $@"
SELECT COUNT(*) 
FROM [{schema}].[{tableName}] 
WHERE [{columnName}] IS NULL";

        // ✅ استخدام الميثود العامة ExecuteScalar<int>
        int count = ExecuteScalar<int>(sql);

        // ✅ تتبع واضح
        if (count > 0)
            Console.WriteLine($"[TRACE:NullCheck] {schema}.{tableName}.{columnName} → Found {count} NULL values");
        else
            Console.WriteLine($"[TRACE:NullCheck] {schema}.{tableName}.{columnName} → No NULL values found");

        return count > 0;
    }


    /// <summary>
    /// Executes a scalar SQL query and returns the result as T.
    /// </summary>
    private T ExecuteScalar<T>(string sql)
    {
        using (var conn = new SqlConnection(_connectionString))
        using (var cmd = new SqlCommand(sql, conn))
        {
            conn.Open();
            object result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value)
                return default(T);
            return (T)Convert.ChangeType(result, typeof(T));
        }
    }

    private bool CanAlterPrimaryKey(EntityDefinition oldEntity, EntityDefinition newEntity)
    {
        // لو مفيش PK في القديم أو الجديد → نعتبره مش اختلاف Identity-only
        if (oldEntity?.PrimaryKey?.Columns == null || newEntity?.PrimaryKey?.Columns == null)
            return true;

        // لو الأعمدة نفسها متطابقة في الاسم والترتيب
        bool sameCols = oldEntity.PrimaryKey?.Columns.SequenceEqual(newEntity.PrimaryKey.Columns, StringComparer.OrdinalIgnoreCase) ?? false;

        if (sameCols)
        {
            var pkColName = newEntity.PrimaryKey.Columns.First();
            var oldCol = oldEntity.Columns.FirstOrDefault(c => c.Name.Equals(pkColName, StringComparison.OrdinalIgnoreCase));
            var newCol = newEntity.Columns.FirstOrDefault(c => c.Name.Equals(pkColName, StringComparison.OrdinalIgnoreCase));

            // ✅ لو الاتنين نفس الـ Identity → مفيش داعي لأي تعديل
            if (oldCol != null && newCol != null && oldCol.IsIdentity == newCol.IsIdentity)
            {
                Console.WriteLine($"⚠️ Skipped PK change for {newEntity.Schema}.{newEntity.Name} because PK columns and Identity match.");
                return false;
            }

            // ✅ لو فيه اختلاف في الـ Identity لكن الجدول فيه بيانات → نتجنب التغيير
            if (oldCol != null && newCol != null && oldCol.IsIdentity != newCol.IsIdentity && !IsTableEmpty(newEntity.Schema, newEntity.Name))
            {
                Console.WriteLine($"⚠️ Skipped PK change for {newEntity.Schema}.{newEntity.Name} because table has data and change is Identity-only.");
                return false;
            }
        }

        return true;
    }



    #endregion
}