using Syn.Core.Logger;
using Syn.Core.SqlSchemaGenerator.Models;

using System.Text;

namespace Syn.Core.SqlSchemaGenerator.Builders;

internal static class TableHelper
{
    internal static void AppendDescriptionForTable(StringBuilder sb, EntityDefinition entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Description))
            return;

        var schema = string.IsNullOrWhiteSpace(entity.Schema) ? "dbo" : entity.Schema;
        var safeDescription = entity.Description.Replace("'", "''");

        var descSql = $@"
IF EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE name = N'MS_Description'
      AND major_id = OBJECT_ID(N'[{schema}].[{entity.Name}]')
      AND minor_id = 0
      AND class = 1
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

        ConsoleLog.Info($"Added MS_Description for table [{schema}].[{entity.Name}]", customPrefix: "TableDescription");
    }


    internal static void AppendDescriptionForColumn(StringBuilder sb, EntityDefinition entity)
    {
        var schema = string.IsNullOrWhiteSpace(entity.Schema) ? "dbo" : entity.Schema;

        foreach (var col in entity.Columns)
        {
            if (string.IsNullOrWhiteSpace(col.Description))
                continue;

            var safeDescription = col.Description.Replace("'", "''");

            var descSql = $@"
IF EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE name = N'MS_Description'
      AND major_id = OBJECT_ID(N'[{schema}].[{entity.Name}]')
      AND minor_id = COLUMNPROPERTY(OBJECT_ID(N'[{schema}].[{entity.Name}]'), '{col.Name}', 'ColumnID')
      AND class = 1
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

            ConsoleLog.Info($"Added MS_Description for column [{col.Name}]", customPrefix: "ColumnDescription");
        }
    }

}
