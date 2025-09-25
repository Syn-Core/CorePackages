using Syn.Core.Logger;
using Syn.Core.SqlSchemaGenerator.Models;

using System.Text;

namespace Syn.Core.SqlSchemaGenerator.Builders;

public partial class SqlCreateTableScriptBuilder
{
    private string BuildCheckConstraints(EntityDefinition entity)
    {
        string schema = string.IsNullOrWhiteSpace(entity.Schema) ? "dbo" : entity.Schema;
        var sb = new StringBuilder();

        foreach (var check in entity.CheckConstraints)
        {
            sb.AppendLine($@"
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints cc
    WHERE cc.name = N'{check.Name}'
      AND cc.parent_object_id = OBJECT_ID(N'[{schema}].[{entity.Name}]')
)
ALTER TABLE [{schema}].[{entity.Name}]
ADD CONSTRAINT [{check.Name}] CHECK ({check.Expression});
GO");
        }

        return sb.ToString();
    }

    private string BuildUniqueConstraints(EntityDefinition entity)
    {
        string schema = string.IsNullOrWhiteSpace(entity.Schema) ? "dbo" : entity.Schema;
        var sb = new StringBuilder();

        foreach (var uq in entity.UniqueConstraints)
        {
            var cols = string.Join(", ", uq.Columns.Select(c => $"[{c}]"));
            sb.AppendLine($@"
IF NOT EXISTS (
    SELECT 1 FROM sys.key_constraints kc
    WHERE kc.name = N'{uq.Name}'
      AND kc.parent_object_id = OBJECT_ID(N'[{schema}].[{entity.Name}]')
)
ALTER TABLE [{schema}].[{entity.Name}]
ADD CONSTRAINT [{uq.Name}] UNIQUE ({cols});
GO");
        }

        return sb.ToString();
    }

    private string BuildIndexes(EntityDefinition entity)
    {
        string schema = string.IsNullOrWhiteSpace(entity.Schema) ? "dbo" : entity.Schema;
        var sb = new StringBuilder();

        foreach (var index in entity.Indexes)
        {
            var cols = string.Join(", ", index.Columns.Select(c => $"[{c}]"));
            var unique = index.IsUnique ? "UNIQUE" : "";

            sb.AppendLine($@"
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes i
    WHERE i.name = N'{index.Name}'
      AND i.object_id = OBJECT_ID(N'[{schema}].[{entity.Name}]')
)
CREATE {unique} INDEX [{index.Name}]
ON [{schema}].[{entity.Name}] ({cols});
GO");
        }

        return sb.ToString();
    }

    private void AppendAllConstraintsForCreate(
    StringBuilder sb,
    EntityDefinition newEntity
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
                    ReferencedColumns = new List<string> { fk.ReferencedColumn ?? "Id" },
                    OnDelete = fk.OnDelete,
                    OnUpdate = fk.OnUpdate
                });

                ConsoleLog.Info(
                    $"[FK-Merge] Added FK constraint '{constraintName}' from ForeignKeys to Constraints",
                    customPrefix: "ConstraintCreate"
                );
            }
        }

        // =========================
        // إنشاء القيود (PK/FK/UNIQUE/DEFAULT/CHECK)
        // =========================
        var processedConstraints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var newConst in newEntity.Constraints)
        {
            if (!processedConstraints.Add(newConst.Name))
                continue;
            var data = TableHelper.BuildAddConstraintSql(newEntity, newConst);
            if (data.StartsWith("-- Unsupported constraint type"))
                continue;
            var addComment = $"Creating {newConst.Type}: {newConst.Name}";
            var addSql = $@"
IF NOT EXISTS (
    SELECT 1 FROM sys.objects 
    WHERE name = N'{newConst.Name}' 
      AND parent_object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    {data}";

            sb.AppendLine($"-- 🆕 {addComment}");
            sb.AppendLine(addSql);
            sb.AppendLine("GO");

            ConsoleLog.Success(addComment, customPrefix: "ConstraintCreate");
        }

        // =========================
        // إنشاء الـ CHECK constraints (لو عندك لوجيك خاص بيها)
        // =========================
        foreach (var ck in newEntity.CheckConstraints)
        {
            var addComment = $"Creating CHECK: {ck.Name}";
            var addSql = $@"
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints 
    WHERE name = N'{ck.Name}' 
      AND parent_object_id = OBJECT_ID(N'[{newEntity.Schema}].[{newEntity.Name}]')
)
    ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] 
        ADD CONSTRAINT [{ck.Name}] CHECK ({ck.Expression});";

            sb.AppendLine($"-- 🆕 {addComment}");
            sb.AppendLine(addSql);
            sb.AppendLine("GO");

            ConsoleLog.Success(addComment, customPrefix: "ConstraintCreate");
        }
    }

}
