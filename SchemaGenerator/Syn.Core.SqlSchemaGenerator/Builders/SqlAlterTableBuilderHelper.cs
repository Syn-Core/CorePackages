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
        EntityDefinition oldEntity,              // 🆕 علشان نقدر نجيب القيود القديمة
        EntityDefinition newEntity,              // 🆕 علشان نقدر نقارن بالقيود الجديدة
        HashSet<string> droppedConstraints       // 🆕 نفس الهاش اللي في Build
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
        EXEC(@sql);  
        FETCH NEXT FROM cur_dc INTO @sql;  
    END
    CLOSE cur_dc; DEALLOCATE cur_dc;");

        // 🆕 تسجيل أسماء DEFAULT constraints
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
        EXEC(@sql);  
        FETCH NEXT FROM cur_ck INTO @sql;  
    END
    CLOSE cur_ck; DEALLOCATE cur_ck;");

        // 🆕 تسجيل أسماء CHECK constraints
        foreach (var chk in oldEntity.CheckConstraints.Where(c => c.ReferencedColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase)))
            droppedConstraints?.Add(chk.Name);

        // 5) حذف العمود القديم
        Add($"    ALTER TABLE {qTable} DROP COLUMN [{columnName}];");

        // 6) إعادة تسمية العمود الجديد
        Add($"    EXEC sp_rename N'{schemaName}.{tableName}.{newCol}', N'{columnName}', 'COLUMN';");

        // 7) فرض NOT NULL لو مطلوب
        if (enforceNotNull)
        {
            Add($"    ALTER TABLE {qTable} ALTER COLUMN [{columnName}] {newColumnType};");
        }

        // 8) 🆕 إعادة إضافة أي قيود CHECK ناقصة
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



    //    public string BuildColumnMigrationScript(
    //    string schemaName,
    //    string tableName,
    //    string columnName,
    //    string newColumnType,             // مثال: "uniqueidentifier NOT NULL" أو "nvarchar(200) NULL"
    //    string? copyExpression = null,    // مثال: "TRY_CONVERT(uniqueidentifier, [Id])" أو null = استخدام [columnName]
    //    string? defaultExpression = null, // مثال: "(NEWID())" أو "(0)" أو null
    //    bool enforceNotNull = true        // لو النوع النهائي NOT NULL
    //)
    //    {
    //        var sb = new StringBuilder();
    //        void Add(string s) => sb.AppendLine(s);

    //        // ملاحظات:
    //        // - نتجنب GO داخل المعاملة.
    //        // - نستعمل جداول متغيرة لتجميع سكربتات إعادة الإنشاء ثم تنفيذها بعد إعادة التسمية.
    //        // - نتعامل مع DEFAULT/CHECK/FKs/INDEXES على العمود كليًا.

    //        var qTable = $"[{schemaName}].[{tableName}]";
    //        var newCol = $"{columnName}_New";

    //        // اشتقاق Nullability النهائية
    //        bool targetNotNull = newColumnType.IndexOf("NOT NULL", StringComparison.OrdinalIgnoreCase) >= 0;

    //        Add($"-- === Safe column migration for {qTable}.{columnName} ===");
    //        Add("SET NOCOUNT ON;");
    //        Add("BEGIN TRY");
    //        Add("    BEGIN TRAN;");

    //        // 1) إضافة العمود الجديد كـ NULL دائمًا مبدئيًا (نجبر NULL) لضمان النسخ حتى لو النهائي NOT NULL
    //        var nullableType = newColumnType.Replace("NOT NULL", "NULL", StringComparison.OrdinalIgnoreCase);
    //        Add($"    -- 1) Add new column as NULLable");
    //        Add($"    IF COL_LENGTH(N'{schemaName}.{tableName}', N'{newCol}') IS NULL");
    //        Add($"        ALTER TABLE {qTable} ADD [{newCol}] {nullableType};");

    //        // 2) نسخ البيانات (تعبير مخصص أو مباشر)
    //        var copyExpr = !string.IsNullOrWhiteSpace(copyExpression)
    //            ? copyExpression
    //            : $"[{columnName}]";
    //        Add($"    -- 2) Copy data from old column to new column");
    //        Add($"    UPDATE tgt SET [{newCol}] = {copyExpr} FROM {qTable} AS tgt;");

    //        // 3) تجميع سكربتات إعادة إنشاء القيود والفهارس والـFKs قبل إسقاطها
    //        Add("    -- 3) Collect recreate scripts for dependencies on old column");
    //        Add("    DECLARE @Recreate TABLE (Seq INT IDENTITY(1,1), Sql NVARCHAR(MAX));");

    //        // 3.a DEFAULT constraints (تجميع ثم إسقاط)
    //        Add("    -- Collect DEFAULT constraints");
    //        Add($@"
    //    INSERT INTO @Recreate(Sql)
    //    SELECT 'ALTER TABLE {qTable} ADD CONSTRAINT [' + dc.name + '] DEFAULT ' + dc.definition + ' FOR [{columnName}]'
    //    FROM sys.default_constraints dc
    //    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    //    WHERE dc.parent_object_id = OBJECT_ID(N'{qTable}') AND c.name = N'{columnName}';");

    //        Add("    -- Drop DEFAULT constraints");
    //        Add($@"
    //    DECLARE @dc NVARCHAR(128);
    //    DECLARE cur_dc CURSOR FAST_FORWARD FOR
    //        SELECT dc.name
    //        FROM sys.default_constraints dc
    //        JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    //        WHERE dc.parent_object_id = OBJECT_ID(N'{qTable}') AND c.name = N'{columnName}';
    //    OPEN cur_dc; FETCH NEXT FROM cur_dc INTO @dc;
    //    WHILE @@FETCH_STATUS = 0
    //    BEGIN
    //        EXEC(N'ALTER TABLE {qTable} DROP CONSTRAINT [' + @dc + ']');
    //        FETCH NEXT FROM cur_dc INTO @dc;
    //    END
    //    CLOSE cur_dc; DEALLOCATE cur_dc;");

    //        // 3.b CHECK constraints (تجميع ثم إسقاط)
    //        Add("    -- Collect CHECK constraints");
    //        Add($@"
    //    INSERT INTO @Recreate(Sql)
    //    SELECT 'ALTER TABLE {qTable} ADD CONSTRAINT [' + cc.name + '] CHECK (' + cc.definition + ')'
    //    FROM sys.check_constraints cc
    //    JOIN sys.columns c ON cc.parent_object_id = c.object_id AND cc.parent_column_id = c.column_id
    //    WHERE cc.parent_object_id = OBJECT_ID(N'{qTable}') AND c.name = N'{columnName}';");

    //        Add("    -- Drop CHECK constraints");
    //        Add($@"
    //    DECLARE @ck NVARCHAR(128);
    //    DECLARE cur_ck CURSOR FAST_FORWARD FOR
    //        SELECT cc.name
    //        FROM sys.check_constraints cc
    //        JOIN sys.columns c ON cc.parent_object_id = c.object_id AND cc.parent_column_id = c.column_id
    //        WHERE cc.parent_object_id = OBJECT_ID(N'{qTable}') AND c.name = N'{columnName}';
    //    OPEN cur_ck; FETCH NEXT FROM cur_ck INTO @ck;
    //    WHILE @@FETCH_STATUS = 0
    //    BEGIN
    //        EXEC(N'ALTER TABLE {qTable} DROP CONSTRAINT [' + @ck + ']');
    //        FETCH NEXT FROM cur_ck INTO @ck;
    //    END
    //    CLOSE cur_ck; DEALLOCATE cur_ck;");

    //        // 3.c الفهارس التي تشترك في العمود (Collect definition ثم Drop)
    //        Add("    -- Collect indexes (including composite and included columns)");
    //        Add($@"
    //    INSERT INTO @Recreate(Sql)
    //    SELECT 
    //        'CREATE ' + CASE WHEN i.is_unique = 1 THEN 'UNIQUE ' ELSE '' END +
    //        CASE WHEN i.type = 1 THEN 'CLUSTERED ' WHEN i.type = 2 THEN 'NONCLUSTERED ' ELSE '' END + 
    //        'INDEX [' + i.name + '] ON {qTable} (' +
    //        STUFF((
    //            SELECT ',[' + c.name + ']' + CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE ' ASC' END
    //            FROM sys.index_columns ic
    //            JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
    //            WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
    //            ORDER BY ic.key_ordinal
    //            FOR XML PATH(''), TYPE).value('.','NVARCHAR(MAX)'),1,1,'') + ')' +
    //        CASE 
    //            WHEN EXISTS(
    //                SELECT 1 FROM sys.index_columns ic 
    //                WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 1
    //            )
    //            THEN ' INCLUDE (' + STUFF((
    //                SELECT ',[' + c.name + ']'
    //                FROM sys.index_columns ic
    //                JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
    //                WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 1
    //                FOR XML PATH(''), TYPE).value('.','NVARCHAR(MAX)'),1,1,'') + ')'
    //            ELSE ''
    //        END +
    //        ISNULL(' WHERE ' + i.filter_definition, '') 
    //    FROM sys.indexes i
    //    WHERE i.object_id = OBJECT_ID(N'{qTable}')
    //      AND i.name IS NOT NULL
    //      AND EXISTS (
    //            SELECT 1
    //            FROM sys.index_columns ic
    //            JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
    //            WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND c.name = N'{columnName}'
    //      );");

    //        Add("    -- Drop indexes touching the column");
    //        Add($@"
    //    DECLARE @ix NVARCHAR(128);
    //    DECLARE cur_ix CURSOR FAST_FORWARD FOR
    //        SELECT DISTINCT i.name
    //        FROM sys.indexes i
    //        JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
    //        JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
    //        WHERE i.object_id = OBJECT_ID(N'{qTable}') AND c.name = N'{columnName}' AND i.name IS NOT NULL;
    //    OPEN cur_ix; FETCH NEXT FROM cur_ix INTO @ix;
    //    WHILE @@FETCH_STATUS = 0
    //    BEGIN
    //        EXEC(N'DROP INDEX [' + @ix + '] ON {qTable}');
    //        FETCH NEXT FROM cur_ix INTO @ix;
    //    END
    //    CLOSE cur_ix; DEALLOCATE cur_ix;");

    //        // 3.d العلاقات الخارجية FK (كطرف Parent أو Child): تجميع وإسقاط وإعادة الإنشاء
    //        Add("    -- Collect FKs where this column participates (parent or child)");
    //        Add($@"
    //    INSERT INTO @Recreate(Sql)
    //    SELECT 
    //        'ALTER TABLE [' + schp.name + '].[' + tp.name + '] ADD CONSTRAINT [' + fk.name + '] FOREIGN KEY (' +
    //        STUFF((
    //            SELECT ',[' + cp.name + ']'
    //            FROM sys.foreign_key_columns fkc2
    //            JOIN sys.columns cp ON fkc2.parent_object_id = cp.object_id AND fkc2.parent_column_id = cp.column_id
    //            WHERE fkc2.constraint_object_id = fk.object_id
    //            ORDER BY fkc2.constraint_column_id
    //            FOR XML PATH(''), TYPE).value('.','NVARCHAR(MAX)'),1,1,'') + ') REFERENCES ' +
    //        '[' + schr.name + '].[' + tr.name + '] (' +
    //        STUFF((
    //            SELECT ',[' + cr.name + ']'
    //            FROM sys.foreign_key_columns fkc3
    //            JOIN sys.columns cr ON fkc3.referenced_object_id = cr.object_id AND fkc3.referenced_column_id = cr.column_id
    //            WHERE fkc3.constraint_object_id = fk.object_id
    //            ORDER BY fkc3.constraint_column_id
    //            FOR XML PATH(''), TYPE).value('.','NVARCHAR(MAX)'),1,1,'') + ')' +
    //        ' ON UPDATE ' + CASE fk.update_referential_action WHEN 0 THEN 'NO ACTION' WHEN 1 THEN 'CASCADE' WHEN 2 THEN 'SET NULL' WHEN 3 THEN 'SET DEFAULT' END +
    //        ' ON DELETE ' + CASE fk.delete_referential_action WHEN 0 THEN 'NO ACTION' WHEN 1 THEN 'CASCADE' WHEN 2 THEN 'SET NULL' WHEN 3 THEN 'SET DEFAULT' END
    //    FROM sys.foreign_keys fk
    //    JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
    //    JOIN sys.tables tp  ON fkc.parent_object_id = tp.object_id
    //    JOIN sys.schemas schp ON tp.schema_id = schp.schema_id
    //    JOIN sys.tables tr  ON fkc.referenced_object_id = tr.object_id
    //    JOIN sys.schemas schr ON tr.schema_id = schr.schema_id
    //    JOIN sys.columns cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
    //    JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id
    //    WHERE (tp.object_id = OBJECT_ID(N'{qTable}') AND cp.name = N'{columnName}')
    //       OR (tr.object_id = OBJECT_ID(N'{qTable}') AND cr.name = N'{columnName}');");

    //        Add("    -- Drop those FKs");
    //        Add($@"
    //    DECLARE @fk NVARCHAR(128);
    //    DECLARE cur_fk CURSOR FAST_FORWARD FOR
    //        SELECT DISTINCT fk.name
    //        FROM sys.foreign_keys fk
    //        JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
    //        JOIN sys.columns cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
    //        JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id
    //        WHERE (fkc.parent_object_id = OBJECT_ID(N'{qTable}') AND cp.name = N'{columnName}')
    //           OR (fkc.referenced_object_id = OBJECT_ID(N'{qTable}') AND cr.name = N'{columnName}');
    //    OPEN cur_fk; FETCH NEXT FROM cur_fk INTO @fk;
    //    WHILE @@FETCH_STATUS = 0
    //    BEGIN
    //        EXEC(N'ALTER TABLE {qTable} DROP CONSTRAINT [' + @fk + ']');
    //        FETCH NEXT FROM cur_fk INTO @fk;
    //    END
    //    CLOSE cur_fk; DEALLOCATE cur_fk;");

    //        // 4) التحقق قبل NOT NULL
    //        if (enforceNotNull && targetNotNull)
    //        {
    //            Add("    -- 4) Enforce NOT NULL: validate/correct NULLs before altering");
    //            if (!string.IsNullOrWhiteSpace(defaultExpression))
    //            {
    //                Add($"    UPDATE {qTable} SET [{newCol}] = {defaultExpression} WHERE [{newCol}] IS NULL;");
    //            }
    //            Add($"    IF EXISTS (SELECT 1 FROM {qTable} WHERE [{newCol}] IS NULL)");
    //            Add("    BEGIN");
    //            Add($"        RAISERROR('Safe column migration aborted: NULL values remain in {tableName}.{newCol}', 16, 1);");
    //            Add("        ROLLBACK TRAN; RETURN;");
    //            Add("    END");
    //        }

    //        // 5) تحويل العمود الجديد إلى NOT NULL لو مطلوب
    //        Add("    -- 5) Set final nullability");
    //        if (targetNotNull)
    //            Add($"    ALTER TABLE {qTable} ALTER COLUMN [{newCol}] {newColumnType};");
    //        else
    //            Add($"    -- Column remains NULLable by requested type: {newColumnType}");

    //        // 6) إسقاط القديم، إعادة التسمية، وإعادة بناء القيود
    //        Add("    -- 6) Swap columns: drop old, rename new to original");
    //        Add($"    IF COL_LENGTH(N'{schemaName}.{tableName}', N'{columnName}') IS NOT NULL");
    //        Add($"        ALTER TABLE {qTable} DROP COLUMN [{columnName}];");
    //        Add($"    EXEC sp_rename N'{schemaName}.{tableName}.{newCol}', N'{columnName}', 'COLUMN';");

    //        // 7) إعادة إنشاء DEFAULT إن طلبنا defaultExpression ولم يكن ضمن التجميع السابق
    //        if (!string.IsNullOrWhiteSpace(defaultExpression))
    //        {
    //            Add("    -- 7) Ensure DEFAULT exists as requested");
    //            Add($@"    IF NOT EXISTS (
    //            SELECT 1 FROM sys.default_constraints dc
    //            JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    //            WHERE dc.parent_object_id = OBJECT_ID(N'{qTable}') AND c.name = N'{columnName}'
    //        )
    //            ALTER TABLE {qTable} ADD CONSTRAINT [DF_{tableName}_{columnName}] DEFAULT {defaultExpression} FOR [{columnName}];");
    //        }

    //        // 8) تنفيذ سكربتات إعادة الإنشاء المجُمّعة (CHECK/DEFAULT/IX/FK) لكن على الاسم بعد إعادة التسمية
    //        Add("    -- 8) Recreate collected dependencies");
    //        Add("    DECLARE @s NVARCHAR(MAX);");
    //        Add("    DECLARE cur_rc CURSOR FAST_FORWARD FOR SELECT Sql FROM @Recreate ORDER BY Seq;");
    //        Add("    OPEN cur_rc; FETCH NEXT FROM cur_rc INTO @s;");
    //        Add("    WHILE @@FETCH_STATUS = 0");
    //        Add("    BEGIN");
    //        Add("        EXEC sp_executesql @s;");
    //        Add("        FETCH NEXT FROM cur_rc INTO @s;");
    //        Add("    END");
    //        Add("    CLOSE cur_rc; DEALLOCATE cur_rc;");

    //        Add("    COMMIT TRAN;");
    //        Add("END TRY");
    //        Add("BEGIN CATCH");
    //        Add("    IF XACT_STATE() <> 0 ROLLBACK TRAN;");
    //        Add("    DECLARE @msg NVARCHAR(4000) = ERROR_MESSAGE();");
    //        Add("    RAISERROR('Safe column migration failed: %s', 16, 1, @msg);");
    //        Add("END CATCH;");
    //        Add("-- === End safe column migration ===");

    //        return sb.ToString();
    //    }


}
