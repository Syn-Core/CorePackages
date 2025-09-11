using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using Syn.Core.SqlSchemaGenerator.Helper;
using Syn.Core.SqlSchemaGenerator.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using static System.Net.Mime.MediaTypeNames;

namespace Syn.Core.SqlSchemaGenerator.Builders;

/// <summary>
/// Generates ALTER TABLE SQL scripts to migrate an existing table definition
/// to match a target definition. Supports columns, indexes, PK/FK/Unique constraints,
/// and Check Constraints.
/// </summary>
public class SqlAlterTableBuilder
{
    private readonly EntityDefinitionBuilder _entityDefinitionBuilder;
    private readonly SqlTableScriptBuilder _tableScriptBuilder;
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance using an <see cref="EntityDefinitionBuilder"/> for schema extraction.
    /// </summary>
    public SqlAlterTableBuilder(EntityDefinitionBuilder entityDefinitionBuilder, string connectionString)
    {
        _entityDefinitionBuilder = entityDefinitionBuilder
            ?? throw new ArgumentNullException(nameof(entityDefinitionBuilder));
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentNullException(nameof(connectionString));
        _tableScriptBuilder = new SqlTableScriptBuilder(entityDefinitionBuilder);
        _connectionString = connectionString;
    }

    /// <summary>
    /// Builds ALTER TABLE SQL script comparing two <see cref="EntityDefinition"/> objects.
    /// </summary>
    /// 
    public string Build(EntityDefinition oldEntity, EntityDefinition newEntity)
    {
        if (oldEntity == null) throw new ArgumentNullException(nameof(oldEntity));
        if (newEntity == null) throw new ArgumentNullException(nameof(newEntity));

        if (oldEntity.Columns.Count == 0 && oldEntity.Constraints.Count == 0)
        {
            var createScript = _tableScriptBuilder.Build(newEntity);
            Console.WriteLine("===== Final Migration Script =====");
            Console.WriteLine(createScript);
            Console.WriteLine("===== End of Script =====");
            return createScript;
        }

        var pkBuilder = new StringBuilder();
        var sbAddColumns = new StringBuilder();
        var sbOtherChanges = new StringBuilder();

        // 🆕 Get migrated PK columns
        var migratedPkColumns = MigratePrimaryKeyIfTypeChanged(pkBuilder, oldEntity, newEntity);

        // 1️⃣ أعمدة جديدة (تجاهل أعمدة الـ PK المهاجرة)
        AppendColumnChanges(sbAddColumns, oldEntity, newEntity, migratedPkColumns);

        // 2️⃣ باقي التغييرات (تجاهل أعمدة الـ PK المهاجرة)
        AppendConstraintChanges(sbOtherChanges, oldEntity, newEntity, migratedPkColumns);
        AppendCheckConstraintChanges(sbOtherChanges, oldEntity, newEntity, migratedPkColumns);
        AppendIndexChanges(sbOtherChanges, oldEntity, newEntity, migratedPkColumns);
        AppendForeignKeyChanges(sbOtherChanges, oldEntity, newEntity, migratedPkColumns);

        // 3️⃣ دمج السكريبت
        var finalScript = new StringBuilder();

        if (pkBuilder.Length > 0)
        {
            finalScript.AppendLine("\n-- ===== Batch 1: Migrate PrimaryKey If Type Changed =====");
            finalScript.Append(pkBuilder);
            finalScript.AppendLine();
        }
        if (sbAddColumns.Length > 0)
        {
            finalScript.AppendLine("\n-- ===== Batch 2: Add new columns =====");
            finalScript.Append(sbAddColumns);
            finalScript.AppendLine("GO");
            finalScript.AppendLine();
        }
        if (sbOtherChanges.Length > 0)
        {
            finalScript.AppendLine("\n-- ===== Batch 3: Other changes =====");
            finalScript.Append(sbOtherChanges);
        }

        Console.WriteLine("===== Final Migration Script =====");
        Console.WriteLine(finalScript.ToString());
        Console.WriteLine("===== End of Script =====");

        return finalScript.ToString();
    }


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
        List<string> excludedColumns) // 🆕 الأعمدة المستثناة
    {
        if (newEntity.NewColumns == null)
            newEntity.NewColumns = new List<string>();

        foreach (var newCol in newEntity.Columns)
        {
            // 🛡️ تخطي الأعمدة المستثناة
            if (excludedColumns != null && excludedColumns.Contains(newCol.Name, StringComparer.OrdinalIgnoreCase))
            {
                var skipComment = $"-- ⏭️ Skipped column: {newCol.Name} (handled in PK migration)";
                sb.AppendLine(skipComment);
                Console.WriteLine(skipComment);
                continue;
            }

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

                Console.WriteLine(comment);
                Console.WriteLine(sql);

                newEntity.NewColumns.Add(newCol.Name);
            }
            else if (!ColumnsAreEquivalent(oldCol, newCol))
            {
                if (CanAlterColumn(oldCol, newCol, newEntity.Schema, newEntity.Name))
                {
                    var comment = $"-- 🔧 Altering column: {newCol.Name}";
                    var sql = BuildAlterColumn(oldCol, newCol, newEntity.Name, newEntity.Schema, newEntity, excludedColumns);

                    sb.AppendLine(comment);
                    sb.AppendLine(sql);

                    Console.WriteLine(comment);
                    Console.WriteLine(sql);
                }
                else
                {
                    var comment = $"-- ⚠️ Recreating column (Drop & Add): {newCol.Name}";
                    sb.AppendLine(comment);
                    Console.WriteLine(comment);

                    var dropSql = $@"
IF EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE Name = N'{newCol.Name}' 
      AND Object_ID = Object_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] DROP COLUMN [{newCol.Name}];";
                    sb.AppendLine(dropSql);
                    Console.WriteLine(dropSql);

                    sb.AppendLine("GO");
                    Console.WriteLine("GO");

                    var addSql = $@"
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE Name = N'{newCol.Name}' 
      AND Object_ID = Object_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] ADD {BuildColumnDefinition(newCol)};";
                    sb.AppendLine(addSql);
                    Console.WriteLine(addSql);
                }
            }
        }

        foreach (var oldCol in oldEntity.Columns)
        {
            // 🛡️ تخطي الأعمدة المستثناة
            if (excludedColumns != null && excludedColumns.Contains(oldCol.Name, StringComparer.OrdinalIgnoreCase))
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
    ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] DROP COLUMN [{oldCol.Name}];";

                sb.AppendLine(comment);
                sb.AppendLine(sql);

                Console.WriteLine(comment);
                Console.WriteLine(sql);
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
            Console.WriteLine($"[TRACE:NullCheck] {schema}.{tableName}.{newCol.Name} → Found {nullCount} NULL values");
            if (nullCount > 0)
            {
                WarnOnce($"{schema}.{tableName}.{newCol.Name}.NotNull",
                    $"⚠️ [ALTER] Skipped {schema}.{tableName}.{newCol.Name} → NOT NULL change unsafe (NULL values exist)");
                return $"-- Skipped ALTER COLUMN for {newCol.Name} due to NULL values in column";
            }
        }
        else if (newCol.IsNullable && !oldCol.IsNullable)
        {
            Console.WriteLine($"[TRACE:NullabilityChange] {schema}.{tableName}.{newCol.Name} → Changed to allow NULL values (safe change)");
        }

        // 🛡️ فحص النوع والطول مع منطق Smart Length Fix
        if (IsTextType(oldCol.TypeName))
        {
            int oldLen = ExtractLengthForIndex(oldCol.TypeName); // -1 = max
            int newLen = ExtractLengthForIndex(newCol.TypeName);

            if (IsMaxType(oldCol.TypeName) && newLen > 0)
            {
                newCol.TypeName = $"nvarchar({newLen})";
                // فحص الفهارس المركبة
                if (newCol.Indexes != null && newCol.Indexes.Count > 0)
                {
                    foreach (var idx in newCol.Indexes.ToList())
                    {
                        int totalBytes = 0;
                        var colSizes = new List<string>();

                        foreach (var colName in idx.Columns)
                        {
                            var colDef = newEntity.Columns
                                .FirstOrDefault(c => c.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));

                            if (colDef != null)
                            {
                                int colBytes = GetColumnMaxLength(colDef.TypeName);
                                if (IsMaxType(colDef.TypeName))
                                    colBytes = GetColumnMaxLength("nvarchar(450)");

                                totalBytes += colBytes;
                                colSizes.Add($"{colDef.Name}={colBytes}B");
                            }
                        }

                        if (totalBytes > 900)
                        {
                            WarnOnce($"{schema}.{tableName}.IDX:{idx.Name}",
                                $"[WARN] {schema}.{tableName} index [{idx.Name}] skipped — total key size {totalBytes} bytes exceeds 900. Columns: {string.Join(", ", colSizes)}");
                            newCol.Indexes.Remove(idx);
                        }
                    }
                }

                if ((newLen * 2) > 900)
                {
                    WarnOnce($"{schema}.{tableName}.{newCol.Name}.Length",
                        $"[WARN] {schema}.{tableName}.{newCol.Name} length {newLen} may exceed index key size limit — index creation skipped, but column length will be updated.");
                    newCol.Indexes?.Clear();
                }
                else
                {
                    Console.WriteLine($"[AUTO-FIX] Changing {schema}.{tableName}.{newCol.Name} from {oldCol.TypeName} to {newCol.TypeName} based on model attribute.");
                }
            }
            else if (IsMaxType(oldCol.TypeName) && newLen == -1 && newCol.Indexes != null && newCol.Indexes.Count > 0)
            {
                int safeLength = 450;
                Console.WriteLine($"[AUTO-FIX] Changing {schema}.{tableName}.{newCol.Name} from nvarchar(max) to nvarchar({safeLength}) for indexing safety.");
                newCol.TypeName = $"nvarchar({safeLength})";
            }
            else if (IsMaxType(oldCol.TypeName) && IsMaxType(newCol.TypeName))
            {
                int safeLength = 450;
                Console.WriteLine($"[AUTO-FIX] Changing {schema}.{tableName}.{newCol.Name} from nvarchar(max) to nvarchar({safeLength}) to match schema standards.");
                newCol.TypeName = $"nvarchar({safeLength})";
            }
            else if (oldLen > newLen && newLen > 0)
            {
                Console.WriteLine($"[TRACE:TypeChange] Reducing length of {schema}.{tableName}.{newCol.Name} from {oldCol.TypeName} to {newCol.TypeName}");
            }
            else if (oldLen != newLen && newLen > 0)
            {
                Console.WriteLine($"[TRACE:TypeChange] Adjusting length of {schema}.{tableName}.{newCol.Name} from {oldCol.TypeName} to {newCol.TypeName}");
            }
        }
        else if (!oldCol.TypeName.Equals(newCol.TypeName, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[TRACE:TypeChange] {schema}.{tableName}.{newCol.Name} → Type change from {oldCol.TypeName} to {newCol.TypeName}");
        }

        // 🛠️ توليد أمر ALTER COLUMN
        var nullable = newCol.IsNullable ? "NULL" : "NOT NULL";
        var alterColumn = $@"
ALTER TABLE [{schema}].[{tableName}]
ALTER COLUMN [{newCol.Name}] {newCol.TypeName} {nullable};";
        // 🛠️ لو فيه Default Value جديدة
        if (newCol.DefaultValue != null)
        {
            alterColumn += $@"

-- Drop old default constraint if exists
DECLARE @dfName NVARCHAR(128);
SELECT @dfName = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON c.default_object_id = dc.object_id
WHERE dc.parent_object_id = OBJECT_ID(N'[{schema}].[{tableName}]')
  AND c.name = '{newCol.Name}';

IF @dfName IS NOT NULL
    EXEC('ALTER TABLE [{schema}].[{tableName}] DROP CONSTRAINT [' + @dfName + ']');

ALTER TABLE [{schema}].[{tableName}]
ADD DEFAULT {newCol.DefaultValue} FOR [{newCol.Name}];";
        }

        // 🛠️ أوامر حذف الـ CHECK constraints المرتبطة بالعمود
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
    EXEC('ALTER TABLE [{schema}].[{tableName}] DROP CONSTRAINT [' + @CheckName + ']');
    FETCH NEXT FROM check_cursor INTO @CheckName;
END
CLOSE check_cursor;
DEALLOCATE check_cursor;
";

        // 🛠️ أوامر حذف الـ Indexes المرتبطة بالعمود (ما عدا الـ PK والـ Unique constraints)
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
    EXEC('DROP INDEX [' + @IndexName + '] ON [{schema}].[{tableName}]');
    FETCH NEXT FROM idx_cursor INTO @IndexName;
END
CLOSE idx_cursor;
DEALLOCATE idx_cursor;
";

        // 🛠️ إرجاع السكريبت النهائي بالترتيب الصحيح
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

    //// 📊 استخراج الطول من النص (nvarchar(600) → 600, nvarchar(max) → -1)
    //private int ExtractLength(string typeName)
    //{
    //    var start = typeName.IndexOf('(');
    //    var end = typeName.IndexOf(')');
    //    if (start > 0 && end > start)
    //    {
    //        var numStr = typeName.Substring(start + 1, end - start - 1);
    //        if (numStr.Equals("max", StringComparison.OrdinalIgnoreCase))
    //            return -1;
    //        if (int.TryParse(numStr, out int len))
    //            return len;
    //    }
    //    return 0; // لو مفيش طول محدد
    //}


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
        List<string> excludedColumns) // 🆕 الأعمدة المستثناة من PK Migration
    {
        var newCols = newEntity.NewColumns ?? new List<string>();

        // أولاً: حذف القيود القديمة أو المعدلة
        foreach (var oldConst in oldEntity.Constraints)
        {
            var match = newEntity.Constraints.FirstOrDefault(c => c.Name == oldConst.Name);
            if (match == null || ConstraintChanged(oldConst, match))
            {
                // 🛡️ لو القيد مرتبط بعمود مستثنى → تخطيه
                if (excludedColumns != null && oldConst.Columns.Any(cn => excludedColumns.Contains(cn, StringComparer.OrdinalIgnoreCase)))
                {
                    var skipMsg = $"-- ⏭️ Skipped dropping constraint {oldConst.Name} because it's related to PK migration column";
                    sb.AppendLine(skipMsg);
                    Console.WriteLine(skipMsg);
                    continue;
                }

                // ✅ فحص أمان للـ PRIMARY KEY
                if (oldConst.Type.Equals("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
                {
                    if (!CanAlterPrimaryKey(oldEntity, newEntity))
                    {
                        var msg = $"-- ⚠️ Skipped dropping PRIMARY KEY {oldConst.Name} due to safety check";
                        sb.AppendLine(msg);
                        Console.WriteLine(msg);
                        continue;
                    }
                }

                var dropComment = $"-- ❌ Dropping constraint: {oldConst.Name}";
                var dropSql = $@"
IF EXISTS (
    SELECT 1 FROM sys.objects 
    WHERE name = N'{oldConst.Name}' 
      AND parent_object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] DROP CONSTRAINT [{oldConst.Name}];";

                sb.AppendLine(dropComment);
                sb.AppendLine(dropSql);
                sb.AppendLine("GO");

                Console.WriteLine(dropComment);
                Console.WriteLine(dropSql);
                Console.WriteLine("GO");
            }
        }

        // ثانياً: إضافة القيود الجديدة أو المعدلة
        foreach (var newConst in newEntity.Constraints)
        {
            var match = oldEntity.Constraints.FirstOrDefault(c => c.Name == newConst.Name);
            if (match == null || ConstraintChanged(match, newConst))
            {
                // 🛡️ لو القيد مرتبط بعمود جديد أو مستثنى → تخطيه
                bool referencesNewColumn = newConst.Columns.Any(colName =>
                    !oldEntity.Columns.Any(c => c.Name.Equals(colName, StringComparison.OrdinalIgnoreCase)));

                bool referencesExcludedColumn = excludedColumns != null &&
                    newConst.Columns.Any(cn => excludedColumns.Contains(cn, StringComparer.OrdinalIgnoreCase));

                if (referencesNewColumn || referencesExcludedColumn)
                {
                    var msg = $"-- Skipped adding constraint {newConst.Name} because it references a new or PK-migrated column";
                    sb.AppendLine(msg);
                    Console.WriteLine(msg);
                    continue;
                }

                // ✅ فحص أمان للـ PRIMARY KEY قبل الإضافة
                if (newConst.Type.Equals("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
                {
                    if (!CanAlterPrimaryKey(oldEntity, newEntity))
                    {
                        var msg = $"-- ⚠️ Skipped adding PRIMARY KEY {newConst.Name} due to safety check";
                        sb.AppendLine(msg);
                        Console.WriteLine(msg);
                        continue;
                    }
                }

                var addComment = $"-- 🆕 Adding constraint: {newConst.Name}";
                var addSql = $@"
IF NOT EXISTS (
    SELECT 1 FROM sys.objects 
    WHERE name = N'{newConst.Name}' 
      AND parent_object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    {BuildAddConstraintSql(newEntity, newConst)}";

                sb.AppendLine(addComment);
                sb.AppendLine(addSql);

                Console.WriteLine(addComment);
                Console.WriteLine(addSql);
            }
        }
    }


    /// <summary>
    /// Determines if a constraint has changed in a meaningful way.
    /// Ignores differences in name casing or column order.
    /// </summary>
    private bool ConstraintChanged(ConstraintDefinition oldConst, ConstraintDefinition newConst)
    {
        // لو نوع القيد نفسه اتغير → تغيير جوهري
        if (!string.Equals(oldConst.Type, newConst.Type, StringComparison.OrdinalIgnoreCase))
            return true;

        // قارن الأعمدة بدون حساسية Case وبغض النظر عن الترتيب
        var oldCols = oldConst.Columns
            .Select(c => c.Trim().ToLowerInvariant())
            .OrderBy(c => c)
            .ToList();

        var newCols = newConst.Columns
            .Select(c => c.Trim().ToLowerInvariant())
            .OrderBy(c => c)
            .ToList();

        if (!oldCols.SequenceEqual(newCols))
            return true;

        // لو القيد من نوع FOREIGN KEY، قارن الأعمدة المرجعية بنفس الطريقة
        if (string.Equals(oldConst.Type, "FOREIGN KEY", StringComparison.OrdinalIgnoreCase))
        {
            var oldRefCols = oldConst.ReferencedColumns
                .Select(c => c.Trim().ToLowerInvariant())
                .OrderBy(c => c)
                .ToList();

            var newRefCols = newConst.ReferencedColumns
                .Select(c => c.Trim().ToLowerInvariant())
                .OrderBy(c => c)
                .ToList();

            if (!oldRefCols.SequenceEqual(newRefCols))
                return true;

            // قارن اسم الجدول المرجعي بدون حساسية Case
            if (!string.Equals(oldConst.ReferencedTable, newConst.ReferencedTable, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // لو وصلنا هنا → مفيش تغيير جوهري
        return false;
    }

    private string BuildAddConstraintSql(EntityDefinition entity, ConstraintDefinition constraint)
    {
        var cols = string.Join(", ", constraint.Columns.Select(c => $"[{c}]"));

        // فحص خاص بالـ PRIMARY KEY
        if (constraint.Type.Equals("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
        {
            // لو الجدول مش فاضى وكان التغيير على الـ Identity بس → نتجنب الإضافة
            if (!IsTableEmpty(entity.Schema, entity.Name))
            {
                Console.WriteLine($"⚠️ Skipped adding PRIMARY KEY [{constraint.Name}] on {entity.Schema}.{entity.Name} because table has data and Identity change is unsafe.");
                return $"-- Skipped adding PRIMARY KEY [{constraint.Name}] due to data safety check";
            }

            return $"ALTER TABLE [{entity.Schema}].[{entity.Name}] ADD CONSTRAINT [{constraint.Name}] PRIMARY KEY ({cols});";
        }

        // فحص خاص بالـ UNIQUE (لو عايز تضيف أمان إضافى)
        if (constraint.Type.Equals("UNIQUE", StringComparison.OrdinalIgnoreCase))
        {
            return $"ALTER TABLE [{entity.Schema}].[{entity.Name}] ADD CONSTRAINT [{constraint.Name}] UNIQUE ({cols});";
        }

        // TODO: دعم FOREIGN KEY
        if (constraint.Type.Equals("FOREIGN KEY", StringComparison.OrdinalIgnoreCase))
        {
            return $"-- TODO: Add FOREIGN KEY definition for [{constraint.Name}]";
        }

        return $"-- Unsupported constraint type: {constraint.Type} for [{constraint.Name}]";
    }
    #endregion

    #region === Check Constraints ===
    private void AppendCheckConstraintChanges(
        StringBuilder sb,
        EntityDefinition oldEntity,
        EntityDefinition newEntity,
        List<string> excludedColumns) // 🆕 الأعمدة المستثناة من PK Migration
    {
        var newCols = newEntity.NewColumns ?? new List<string>();

        // أولاً: حذف الـ CHECK constraints القديمة أو المعدلة
        foreach (var oldCheck in oldEntity.CheckConstraints)
        {
            var match = newEntity.CheckConstraints.FirstOrDefault(c => c.Name == oldCheck.Name);
            bool changed = match == null ||
                           !string.Equals(Normalize(match.Expression), Normalize(oldCheck.Expression), StringComparison.OrdinalIgnoreCase);

            if (changed)
            {
                // 🛡️ تخطي لو القيد مرتبط بعمود مستثنى
                if (excludedColumns != null && oldCheck.ReferencedColumns.Any(cn => excludedColumns.Contains(cn, StringComparer.OrdinalIgnoreCase)))
                {
                    var skipMsg = $"-- ⏭️ Skipped dropping CHECK {oldCheck.Name} because it's related to PK migration column";
                    sb.AppendLine(skipMsg);
                    Console.WriteLine(skipMsg);
                    continue;
                }

                var dropComment = $"-- ❌ Dropping CHECK: {oldCheck.Name}";
                var dropSql = $@"
IF EXISTS (
    SELECT 1 FROM sys.check_constraints cc
    WHERE cc.name = N'{oldCheck.Name}'
      AND cc.parent_object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] DROP CONSTRAINT [{oldCheck.Name}];";

                sb.AppendLine(dropComment);
                sb.AppendLine(dropSql);
                sb.AppendLine("GO");

                Console.WriteLine(dropComment);
                Console.WriteLine(dropSql);
                Console.WriteLine("GO");
            }
        }

        // ثانياً: إضافة الـ CHECK constraints الجديدة أو المعدلة
        foreach (var newCheck in newEntity.CheckConstraints)
        {
            var match = oldEntity.CheckConstraints.FirstOrDefault(c => c.Name == newCheck.Name);
            bool changed = match == null ||
                           !string.Equals(Normalize(match.Expression), Normalize(newCheck.Expression), StringComparison.OrdinalIgnoreCase);

            if (changed)
            {
                // 🛡️ تخطي لو القيد مرتبط بعمود جديد أو مستثنى
                bool referencesNewColumn = newCheck.ReferencedColumns.Any(colName =>
                    !oldEntity.Columns.Any(c => c.Name.Equals(colName, StringComparison.OrdinalIgnoreCase)));

                bool referencesExcludedColumn = excludedColumns != null &&
                    newCheck.ReferencedColumns.Any(cn => excludedColumns.Contains(cn, StringComparer.OrdinalIgnoreCase));

                if (referencesNewColumn || referencesExcludedColumn)
                {
                    var msg = $"-- Skipped adding CHECK {newCheck.Name} because it references a new or PK-migrated column";
                    sb.AppendLine(msg);
                    Console.WriteLine(msg);
                    continue;
                }

                var addComment = $"-- 🆕 Adding CHECK: {newCheck.Name}";
                var addSql = $@"
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints cc
    WHERE cc.name = N'{newCheck.Name}'
      AND cc.parent_object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] ADD CONSTRAINT [{newCheck.Name}] CHECK ({newCheck.Expression});";

                sb.AppendLine(addComment);
                sb.AppendLine(addSql);

                Console.WriteLine(addComment);
                Console.WriteLine(addSql);
            }
        }
    }

    private string Normalize(string input) =>
        input?.Trim().Replace("(", "").Replace(")", "").Replace(" ", "") ?? string.Empty;
    #endregion

    #region === Indexes ===

    private void AppendIndexChanges(
        StringBuilder sb,
        EntityDefinition oldEntity,
        EntityDefinition newEntity,
        List<string> excludedColumns) // 🆕 الأعمدة المستثناة من PK Migration
    {
        Console.WriteLine($"[DEBUG] OldEntity.Indexes ({oldEntity.Indexes.Count}):");
        foreach (var idx in oldEntity.Indexes)
            Console.WriteLine($"    - {idx.Name} | Unique={idx.IsUnique} | Cols=[{string.Join(", ", idx.Columns)}]");

        Console.WriteLine($"[DEBUG] NewEntity.Indexes ({newEntity.Indexes.Count}):");
        foreach (var idx in newEntity.Indexes)
            Console.WriteLine($"    - {idx.Name} | Unique={idx.IsUnique} | Cols=[{string.Join(", ", idx.Columns)}]");

        // 🗑️ فحص الفهارس المحذوفة
        foreach (var oldIdx in oldEntity.Indexes)
        {
            // 🛡️ تخطي لو الفهرس مرتبط بعمود مستثنى
            if (excludedColumns != null && oldIdx.Columns.Any(cn => excludedColumns.Contains(cn, StringComparer.OrdinalIgnoreCase)))
            {
                var skipMsg = $"-- ⏭️ Skipped dropping index {oldIdx.Name} because it's related to PK migration column";
                sb.AppendLine(skipMsg);
                Console.WriteLine(skipMsg);
                continue;
            }

            if (!newEntity.Indexes.Any(i => i.Name.Equals(oldIdx.Name, StringComparison.OrdinalIgnoreCase)))
            {
                var dropComment = $"-- ❌ Dropping index: {oldIdx.Name}";
                var dropSql = $@"
IF EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE name = N'{oldIdx.Name}' 
      AND object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    DROP INDEX [{oldIdx.Name}] ON [{newEntity.Schema}].[{newEntity.Name}];";

                sb.AppendLine(dropComment);
                sb.AppendLine(dropSql);

                Console.WriteLine(dropComment);
                Console.WriteLine(dropSql);
            }
        }

        // 🆕 فحص الفهارس الجديدة أو المعدلة
        foreach (var newIdx in newEntity.Indexes)
        {
            // 🛡️ تخطي لو الفهرس مرتبط بعمود مستثنى
            if (excludedColumns != null && newIdx.Columns.Any(cn => excludedColumns.Contains(cn, StringComparer.OrdinalIgnoreCase)))
            {
                var skipMsg = $"-- ⏭️ Skipped creating index {newIdx.Name} because it's related to PK migration column";
                sb.AppendLine(skipMsg);
                Console.WriteLine(skipMsg);
                continue;
            }

            var existingIdx = oldEntity.Indexes.FirstOrDefault(i =>
                i.Name.Equals(newIdx.Name, StringComparison.OrdinalIgnoreCase));

            bool indexIsNewOrChanged =
                existingIdx == null ||
                !existingIdx.Columns.SequenceEqual(newIdx.Columns, StringComparer.OrdinalIgnoreCase) ||
                existingIdx.IsUnique != newIdx.IsUnique ||
                !string.Equals(existingIdx.FilterExpression ?? "", newIdx.FilterExpression ?? "", StringComparison.OrdinalIgnoreCase) ||
                !(existingIdx.IncludeColumns ?? new List<string>())
                    .SequenceEqual(newIdx.IncludeColumns ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

            if (!indexIsNewOrChanged)
                continue;

            bool skipIndex = false;
            int totalBytes = 0;

            foreach (var colName in newIdx.Columns)
            {
                var colDef = newEntity.Columns.FirstOrDefault(c =>
                    c.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));

                if (colDef != null)
                {
                    bool existsInDb = oldEntity.Columns.Any(c =>
                        c.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));

                    if (!existsInDb)
                    {
                        var msg = $"-- Skipped creating index [{newIdx.Name}] because column {colName} is new in this migration";
                        sb.AppendLine(msg);
                        Console.WriteLine(msg);
                        skipIndex = true;
                        break;
                    }

                    if (colDef.TypeName.Contains("max", StringComparison.OrdinalIgnoreCase) ||
                        colDef.TypeName.Contains("text", StringComparison.OrdinalIgnoreCase) ||
                        colDef.TypeName.Contains("ntext", StringComparison.OrdinalIgnoreCase) ||
                        colDef.TypeName.Contains("image", StringComparison.OrdinalIgnoreCase) ||
                        (colDef.TypeName.StartsWith("nvarchar", StringComparison.OrdinalIgnoreCase) &&
                         int.TryParse(new string(colDef.TypeName.Where(char.IsDigit).ToArray()), out var len) &&
                         len > 450))
                    {
                        var msg = $"-- Skipped creating index [{newIdx.Name}] because column {colName} type {colDef.TypeName} not indexable";
                        sb.AppendLine(msg);
                        Console.WriteLine(msg);
                        skipIndex = true;
                        break;
                    }

                    int colBytes = colDef.TypeName.Contains("(max)", StringComparison.OrdinalIgnoreCase)
                        ? GetColumnMaxLength("nvarchar(450)")
                        : GetColumnMaxLength(colDef.TypeName);

                    totalBytes += colBytes;
                }
            }

            if (!skipIndex && totalBytes > 900)
            {
                var msg = $"-- Skipped creating index [{newIdx.Name}] due to total key size {totalBytes} bytes exceeding 900-byte index key limit";
                sb.AppendLine(msg);
                Console.WriteLine(msg);
                skipIndex = true;
            }

            if (skipIndex) continue;

            // 🛠️ لو الفهرس موجود لكن مختلف → Drop + GO قبل الـ Create
            if (existingIdx != null)
            {
                var dropComment = $"-- ❌ Dropping index: {existingIdx.Name} (to recreate with changes)";
                var dropSql = $@"
IF EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE name = N'{existingIdx.Name}' 
      AND object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    DROP INDEX [{existingIdx.Name}] ON [{newEntity.Schema}].[{newEntity.Name}];";

                sb.AppendLine(dropComment);
                sb.AppendLine(dropSql);
                sb.AppendLine("GO");

                Console.WriteLine(dropComment);
                Console.WriteLine(dropSql);
                Console.WriteLine("GO");
            }

            var cols = string.Join(", ", newIdx.Columns.Select(c => $"[{c}]"));
            var unique = newIdx.IsUnique ? "UNIQUE " : "";

            var createComment = $"-- 🆕 Creating index: {newIdx.Name}";
            var createSql = $@"
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE name = N'{newIdx.Name}' 
      AND object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    CREATE {unique}INDEX [{newIdx.Name}] ON [{newEntity.Schema}].[{newEntity.Name}] ({cols});";

            sb.AppendLine(createComment);
            sb.AppendLine(createSql);

            Console.WriteLine(createComment);
            Console.WriteLine(createSql);

            if (!string.IsNullOrWhiteSpace(newIdx.Description))
            {
                var descSql = $@"
EXEC sys.sp_addextendedproperty 
    @name = N'MS_Description',
    @value = N'{newIdx.Description}',
    @level0type = N'SCHEMA',    @level0name = N'{newEntity.Schema}',
    @level1type = N'TABLE',     @level1name = N'{newEntity.Name}',
    @level2type = N'INDEX',     @level2name = N'{newIdx.Name}';";

                sb.AppendLine(descSql);
                Console.WriteLine(descSql);
            }
        }
    }


    private List<string> MigratePrimaryKeyIfTypeChanged(StringBuilder sb, EntityDefinition oldEntity, EntityDefinition newEntity)
    {
        var migratedColumns = new List<string>();

        if (oldEntity.PrimaryKey == null || newEntity.PrimaryKey == null)
            return migratedColumns;

        var oldPkName = oldEntity.PrimaryKey.Columns.FirstOrDefault();
        var newPkName = newEntity.PrimaryKey.Columns.FirstOrDefault();

        if (string.IsNullOrEmpty(oldPkName) || string.IsNullOrEmpty(newPkName))
            return migratedColumns;

        var oldPkCol = oldEntity.Columns.FirstOrDefault(c => c.Name.Equals(oldPkName, StringComparison.OrdinalIgnoreCase));
        var newPkCol = newEntity.Columns.FirstOrDefault(c => c.Name.Equals(newPkName, StringComparison.OrdinalIgnoreCase));

        if (oldPkCol == null || newPkCol == null)
            return migratedColumns;

        if (oldPkName.Equals(newPkName, StringComparison.OrdinalIgnoreCase) &&
            oldPkCol.PropertyType != newPkCol.PropertyType)
        {
            var sqlType = newPkCol.PropertyType.MapClrTypeToSql();
            var newColumnType = $"{sqlType} NOT NULL";

            var migrationScript = BuildPkMigrationScript(newEntity.Schema ?? "dbo", newEntity.Name, oldPkName, oldEntity.PrimaryKey.Name, newColumnType);

            sb.AppendLine($"-- 🆕 PK type changed from {oldPkCol.Name} to {newPkCol.Name}");
            sb.AppendLine(migrationScript);

            Console.WriteLine($"-- 🆕 PK type changed from {oldPkCol.Name} to {newPkCol.Name}");
            Console.WriteLine(migrationScript);

            migratedColumns.Add(oldPkName);
        }

        return migratedColumns;
    }

    private string BuildPkMigrationScript(
        string schemaName,
        string tableName,
        string pkColumnName,
        string pkConstraintName,
        string newColumnType)
    {
        var sb = new StringBuilder();

        void Add(string line)
        {
            sb.AppendLine(line);
            Console.WriteLine(line);
        }

        // 1️⃣ Add new PK column as NULLable
        Add("-- 1️⃣ Add new PK column as NULLable");
        var nullableType = newColumnType.Replace("NOT NULL", "NULL");
        Add($"ALTER TABLE [{schemaName}].[{tableName}] ADD [{pkColumnName}_New] {nullableType};");
        Add("GO");

        // 2️⃣ Copy data from old PK column to new PK column
        Add("-- 2️⃣ Copy data from old PK column to new PK column");
        Add($"UPDATE [{schemaName}].[{tableName}] SET [{pkColumnName}_New] = [{pkColumnName}];");
        Add("GO");

        // 3️⃣ Alter new PK column to NOT NULL
        Add("-- 3️⃣ Alter new PK column to NOT NULL");
        Add($"ALTER TABLE [{schemaName}].[{tableName}] ALTER COLUMN [{pkColumnName}_New] {newColumnType};");
        Add("GO");

        // 4️⃣ Update related FKs
        Add("-- 4️⃣ Update related FKs");
        Add("DECLARE @SQL NVARCHAR(MAX);");
        Add("DECLARE fk_cursor CURSOR FOR");
        Add($@"
SELECT 
    fk.name AS FK_Name,
    tp.name AS ParentTable,
    cp.name AS ParentColumn
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
JOIN sys.tables tp ON fkc.parent_object_id = tp.object_id
JOIN sys.columns cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
JOIN sys.tables tr ON fkc.referenced_object_id = tr.object_id
JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id
JOIN sys.schemas sp ON tp.schema_id = sp.schema_id
JOIN sys.schemas sr ON tr.schema_id = sr.schema_id
WHERE tr.name = '{tableName}'
  AND sr.name = '{schemaName}'
  AND cr.name = '{pkColumnName}';
");

        Add("DECLARE @FKName SYSNAME, @ParentTable SYSNAME, @ParentColumn SYSNAME;");
        Add("OPEN fk_cursor;");
        Add("FETCH NEXT FROM fk_cursor INTO @FKName, @ParentTable, @ParentColumn;");

        Add("WHILE @@FETCH_STATUS = 0");
        Add("BEGIN");
        Add("    PRINT 'Updating FK: ' + @FKName + ' in table ' + @ParentTable;");
        Add("    SET @SQL = 'ALTER TABLE [' + @ParentTable + '] DROP CONSTRAINT [' + @FKName + ']';");
        Add("    EXEC sp_executesql @SQL;");
        Add($"    SET @SQL = 'UPDATE p SET p.[' + @ParentColumn + '] = c.[{pkColumnName}_New] " +
            $"FROM [' + @ParentTable + '] p JOIN [{schemaName}].[{tableName}] c " +
            $"ON p.[' + @ParentColumn + '] = c.[{pkColumnName}]';");
        Add("    EXEC sp_executesql @SQL;");
        Add($"    SET @SQL = 'ALTER TABLE [' + @ParentTable + '] ADD CONSTRAINT [' + @FKName + '] " +
            $"FOREIGN KEY ([' + @ParentColumn + ']) REFERENCES [{schemaName}].[{tableName}]([{pkColumnName}_New])';");
        Add("    EXEC sp_executesql @SQL;");
        Add("    FETCH NEXT FROM fk_cursor INTO @FKName, @ParentTable, @ParentColumn;");
        Add("END");
        Add("CLOSE fk_cursor;");
        Add("DEALLOCATE fk_cursor;");
        Add("GO");

        // 🔥 Drop dependent CHECK constraints dynamically
        Add("-- 🔥 Drop dependent CHECK constraints on old PK column");
        Add($@"
DECLARE @CheckName SYSNAME;
DECLARE check_cursor CURSOR FOR
SELECT cc.name
FROM sys.check_constraints cc
JOIN sys.columns col ON cc.parent_object_id = col.object_id
    AND cc.parent_column_id = col.column_id
JOIN sys.tables t ON col.object_id = t.object_id
JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE t.name = '{tableName}'
  AND s.name = '{schemaName}'
  AND col.name = '{pkColumnName}';

OPEN check_cursor;
FETCH NEXT FROM check_cursor INTO @CheckName;
WHILE @@FETCH_STATUS = 0
BEGIN
    PRINT 'Dropping CHECK constraint: ' + @CheckName;
    EXEC('ALTER TABLE [{schemaName}].[{tableName}] DROP CONSTRAINT [' + @CheckName + ']');
    FETCH NEXT FROM check_cursor INTO @CheckName;
END
CLOSE check_cursor;
DEALLOCATE check_cursor;
GO
");

        // 5️⃣ Drop old PK constraint
        Add("-- 5️⃣ Drop old PK constraint");
        Add($"ALTER TABLE [{schemaName}].[{tableName}] DROP CONSTRAINT [{pkConstraintName}];");
        Add("GO");

        // 6️⃣ Drop old PK column
        Add("-- 6️⃣ Drop old PK column");
        Add($"ALTER TABLE [{schemaName}].[{tableName}] DROP COLUMN [{pkColumnName}];");
        Add("GO");

        // 7️⃣ Rename new PK column to original name
        Add("-- 7️⃣ Rename new PK column to original name");
        Add($"EXEC sp_rename '{schemaName}.{tableName}.{pkColumnName}_New', '{pkColumnName}', 'COLUMN';");
        Add("GO");

        // 8️⃣ Recreate PK constraint
        Add("-- 8️⃣ Recreate PK constraint");
        Add($"ALTER TABLE [{schemaName}].[{tableName}] ADD CONSTRAINT [{pkConstraintName}] PRIMARY KEY ([{pkColumnName}]);");
        Add("GO");

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
        List<string> excludedColumns) // 🆕 الأعمدة المستثناة من PK Migration
    {
        var newCols = newEntity.NewColumns ?? new List<string>();

        var oldFks = oldEntity.Constraints.Where(c => c.Type == "FOREIGN KEY").ToList();
        var newFks = newEntity.Constraints.Where(c => c.Type == "FOREIGN KEY").ToList();

        // ❌ حذف العلاقات القديمة
        foreach (var oldFk in oldFks)
        {
            // 🛡️ تخطي لو الـ FK مرتبط بعمود مستثنى
            if (excludedColumns != null && oldFk.Columns.Any(cn => excludedColumns.Contains(cn, StringComparer.OrdinalIgnoreCase)))
            {
                var skipMsg = $"-- ⏭️ Skipped dropping FK {oldFk.Name} because it's related to PK migration column";
                sb.AppendLine(skipMsg);
                Console.WriteLine(skipMsg);
                continue;
            }

            if (!newFks.Any(f => f.Name.Equals(oldFk.Name, StringComparison.OrdinalIgnoreCase)))
            {
                var dropComment = $"-- ❌ Dropping FK: {oldFk.Name}";
                var dropSql = $@"
IF EXISTS (
    SELECT 1 FROM sys.foreign_keys 
    WHERE name = N'{oldFk.Name}' 
      AND parent_object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] DROP CONSTRAINT [{oldFk.Name}];";

                sb.AppendLine(dropComment);
                sb.AppendLine(dropSql);
                sb.AppendLine("GO");

                Console.WriteLine(dropComment);
                Console.WriteLine(dropSql);
                Console.WriteLine("GO");
            }
        }

        // 🆕 إضافة أو تعديل العلاقات الجديدة
        foreach (var newFk in newFks)
        {
            // 🛡️ تخطي لو الـ FK مرتبط بعمود مستثنى
            if (excludedColumns != null && newFk.Columns.Any(cn => excludedColumns.Contains(cn, StringComparer.OrdinalIgnoreCase)))
            {
                var skipMsg = $"-- ⏭️ Skipped adding FK {newFk.Name} because it's related to PK migration column";
                sb.AppendLine(skipMsg);
                Console.WriteLine(skipMsg);
                continue;
            }

            var match = oldFks.FirstOrDefault(f => f.Name == newFk.Name);
            var changed = match == null
                || !string.Equals(match.ReferencedTable, newFk.ReferencedTable, StringComparison.OrdinalIgnoreCase)
                || !match.Columns.SequenceEqual(newFk.Columns)
                || !match.ReferencedColumns.SequenceEqual(newFk.ReferencedColumns);

            if (!changed)
                continue;

            // 🛡️ لو أي عمود في الـ FK جديد فعلاً → تخطيه
            bool referencesNewColumn = newFk.Columns.Any(colName =>
                !oldEntity.Columns.Any(c => c.Name.Equals(colName, StringComparison.OrdinalIgnoreCase)));

            if (referencesNewColumn)
            {
                var msg = $"-- Skipped adding FK {newFk.Name} because it references a new column in this migration";
                sb.AppendLine(msg);
                Console.WriteLine(msg);
                continue;
            }

            // 🛠️ لو الـ FK موجود لكن مختلف → Drop + GO قبل الـ Add
            if (match != null)
            {
                var dropComment = $"-- ❌ Dropping FK: {match.Name} (to recreate with changes)";
                var dropSql = $@"
IF EXISTS (
    SELECT 1 FROM sys.foreign_keys 
    WHERE name = N'{match.Name}' 
      AND parent_object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] DROP CONSTRAINT [{match.Name}];";

                sb.AppendLine(dropComment);
                sb.AppendLine(dropSql);
                sb.AppendLine("GO");

                Console.WriteLine(dropComment);
                Console.WriteLine(dropSql);
                Console.WriteLine("GO");
            }

            var cols = string.Join(", ", newFk.Columns.Select(c => $"[{c}]"));
            var refCols = string.Join(", ", newFk.ReferencedColumns.Select(c => $"[{c}]"));

            var addComment = $"-- 🆕 Adding FK: {newFk.Name}";
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

            sb.AppendLine(addComment);
            sb.AppendLine(addSql);

            Console.WriteLine(addComment);
            Console.WriteLine(addSql);
        }
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