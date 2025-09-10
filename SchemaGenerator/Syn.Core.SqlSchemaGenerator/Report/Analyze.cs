using Syn.Core.SqlSchemaGenerator.Models;

using System.Text.RegularExpressions;

namespace Syn.Core.SqlSchemaGenerator.Report;

public static class Analyze
{
    /// <summary>
    /// Compares two EntityDefinitions and returns a list of ImpactItems describing the differences.
    /// </summary>
    /// <param name="oldEntity">The entity definition from the current database schema.</param>
    /// <param name="newEntity">The entity definition from the new model.</param>
    /// <returns>List of ImpactItem describing schema changes.</returns>
    public static List<ImpactItem> AnalyzeImpact(EntityDefinition oldEntity, EntityDefinition newEntity)
    {
        var impact = new List<ImpactItem>();

        AnalyzeColumns(oldEntity, newEntity, impact);
        AnalyzeConstraints(oldEntity, newEntity, impact); // PK, FK, UNIQUE handled here
        AnalyzeCheckConstraints(oldEntity, newEntity, impact);
        AnalyzeIndexes(oldEntity, newEntity, impact);
        AnalyzeDefaultConstraints(oldEntity, newEntity, impact);

        return impact;
    }

    #region Columns
    private static void AnalyzeColumns(EntityDefinition oldEntity, EntityDefinition newEntity, List<ImpactItem> impact)
    {
        var oldColumns = oldEntity?.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase) ?? new();
        var newColumns = newEntity?.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase) ?? new();

        foreach (var kvp in newColumns)
        {
            var name = kvp.Key;
            var newCol = kvp.Value;

            if (!oldColumns.ContainsKey(name))
            {
                impact.Add(new ImpactItem { Type = "Column", Action = "Added", Table = newEntity.Name, Name = name });
            }
            else
            {
                var oldCol = oldColumns[name];
                if (oldCol.TypeName != newCol.TypeName || oldCol.IsNullable != newCol.IsNullable)
                {
                    impact.Add(new ImpactItem
                    {
                        Type = "Column",
                        Action = "Modified",
                        Table = newEntity.Name,
                        Name = name,
                        OriginalType = $"{oldCol.TypeName} {(oldCol.IsNullable ? "NULL" : "NOT NULL")}",
                        NewType = $"{newCol.TypeName} {(newCol.IsNullable ? "NULL" : "NOT NULL")}"
                    });
                }
            }
        }

        foreach (var name in oldColumns.Keys)
        {
            if (!newColumns.ContainsKey(name))
            {
                impact.Add(new ImpactItem { Type = "Column", Action = "Dropped", Table = oldEntity.Name, Name = name });
            }
        }
    }
    #endregion

    #region Constraints (PK, FK, UNIQUE, etc.)
    /// <summary>
    /// Compares constraints (Primary Key, Foreign Key, Unique, etc.) between old and new entities.
    /// </summary>

    private static void AnalyzeConstraints(EntityDefinition oldEntity, EntityDefinition newEntity, List<ImpactItem> impact)
    {
        var oldConstraints = oldEntity?.Constraints.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase) ?? new();
        var newConstraints = newEntity?.Constraints.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase) ?? new();

        foreach (var kvp in newConstraints)
        {
            var name = kvp.Key;
            var newConstraint = kvp.Value;

            if (!oldConstraints.ContainsKey(name))
            {
                impact.Add(new ImpactItem
                {
                    Type = newConstraint.Type,
                    Action = "Added",
                    Table = newEntity.Name,
                    Name = name,
                    NewType = BuildConstraintDetails(newConstraint)
                });
            }
            else
            {
                var oldConstraint = oldConstraints[name];
                var oldDetails = BuildConstraintDetails(oldConstraint);
                var newDetails = BuildConstraintDetails(newConstraint);

                if (!string.Equals(oldDetails, newDetails, StringComparison.OrdinalIgnoreCase))
                {
                    impact.Add(new ImpactItem
                    {
                        Type = newConstraint.Type,
                        Action = "Modified",
                        Table = newEntity.Name,
                        Name = name,
                        OriginalType = oldDetails,
                        NewType = newDetails
                    });
                }
            }
        }

        foreach (var name in oldConstraints.Keys)
        {
            if (!newConstraints.ContainsKey(name))
            {
                var oldConstraint = oldConstraints[name];
                impact.Add(new ImpactItem
                {
                    Type = oldConstraint.Type,
                    Action = "Dropped",
                    Table = oldEntity.Name,
                    Name = name,
                    OriginalType = BuildConstraintDetails(oldConstraint)
                });
            }
        }
    }
    #endregion

    #region Check Constraints
    private static void AnalyzeCheckConstraints(EntityDefinition oldEntity, EntityDefinition newEntity, List<ImpactItem> impact)
    {
        var oldChecks = oldEntity?.CheckConstraints.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase) ?? new();
        var newChecks = newEntity?.CheckConstraints.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase) ?? new();

        foreach (var kvp in newChecks)
        {
            var name = kvp.Key;
            var newCheck = kvp.Value;

            if (!oldChecks.ContainsKey(name))
            {
                impact.Add(new ImpactItem
                {
                    Type = "CheckConstraint",
                    Action = "Added",
                    Table = newEntity.Name,
                    Name = name,
                    NewType = NormalizeExpression(newCheck.Expression) // عرض الـ Expression الجديدة
                });
            }
            else
            {
                var oldCheck = oldChecks[name];
                var oldExpr = NormalizeExpression(oldCheck.Expression);
                var newExpr = NormalizeExpression(newCheck.Expression);

                if (!string.Equals(oldExpr, newExpr, StringComparison.OrdinalIgnoreCase))
                {
                    impact.Add(new ImpactItem
                    {
                        Type = "CheckConstraint",
                        Action = "Modified",
                        Table = newEntity.Name,
                        Name = name,
                        OriginalType = oldExpr,
                        NewType = newExpr
                    });
                }
            }
        }

        foreach (var name in oldChecks.Keys)
        {
            if (!newChecks.ContainsKey(name))
            {
                var oldCheck = oldChecks[name];
                impact.Add(new ImpactItem
                {
                    Type = "CheckConstraint",
                    Action = "Dropped",
                    Table = oldEntity.Name,
                    Name = name,
                    OriginalType = NormalizeExpression(oldCheck.Expression) // عرض الـ Expression القديمة
                });
            }
        }
    }

    #endregion

    #region Indexes
    private static void AnalyzeIndexes(EntityDefinition oldEntity, EntityDefinition newEntity, List<ImpactItem> impact)
    {
        var oldIndexes = oldEntity?.Indexes.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase) ?? new();
        var newIndexes = newEntity?.Indexes.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase) ?? new();

        foreach (var kvp in newIndexes)
        {
            var name = kvp.Key;
            var newIndex = kvp.Value;

            if (!oldIndexes.ContainsKey(name))
            {
                impact.Add(new ImpactItem
                {
                    Type = "Index",
                    Action = "Added",
                    Table = newEntity.Name,
                    Name = name,
                    NewType = $"{string.Join(", ", newIndex.Columns)}" + (newIndex.IsUnique ? " [UNIQUE]" : "")
                });
            }
            else
            {
                var oldIndex = oldIndexes[name];
                var oldCols = string.Join(", ", oldIndex.Columns);
                var newCols = string.Join(", ", newIndex.Columns);

                bool modified = false;
                string? originalVal = null;
                string? newVal = null;

                if (!oldIndex.Columns.SequenceEqual(newIndex.Columns, StringComparer.OrdinalIgnoreCase) ||
                    oldIndex.IsUnique != newIndex.IsUnique)
                {
                    modified = true;
                    originalVal = $"{oldCols}" + (oldIndex.IsUnique ? " [UNIQUE]" : "");
                    newVal = $"{newCols}" + (newIndex.IsUnique ? " [UNIQUE]" : "");
                }

                if (modified)
                {
                    impact.Add(new ImpactItem
                    {
                        Type = "Index",
                        Action = "Modified",
                        Table = newEntity.Name,
                        Name = name,
                        OriginalType = originalVal,
                        NewType = newVal
                    });
                }
            }
        }

        foreach (var name in oldIndexes.Keys)
        {
            if (!newIndexes.ContainsKey(name))
            {
                var oldIndex = oldIndexes[name];
                impact.Add(new ImpactItem
                {
                    Type = "Index",
                    Action = "Dropped",
                    Table = oldEntity.Name,
                    Name = name,
                    OriginalType = $"{string.Join(", ", oldIndex.Columns)}" + (oldIndex.IsUnique ? " [UNIQUE]" : "")
                });
            }
        }
    }
    #endregion

    #region Default Constraints
    private static void AnalyzeDefaultConstraints(EntityDefinition oldEntity, EntityDefinition newEntity, List<ImpactItem> impact)
    {
        var oldDefaults = oldEntity?.Constraints.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase) ?? new();
        var newDefaults = newEntity?.Constraints.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase) ?? new();

        foreach (var kvp in newDefaults)
        {
            if (!oldDefaults.ContainsKey(kvp.Key))
            {
                impact.Add(new ImpactItem { Type = "DefaultConstraint", Action = "Added", Table = newEntity.Name, Name = kvp.Key });
            }
            else
            {
                var oldDef = oldDefaults[kvp.Key];
                var newDef = kvp.Value;
                if (!string.Equals(NormalizeExpression(oldDef.DefaultValue), NormalizeExpression(newDef.DefaultValue), StringComparison.OrdinalIgnoreCase))
                {
                    impact.Add(new ImpactItem { Type = "DefaultConstraint", Action = "Modified", Table = newEntity.Name, Name = kvp.Key });
                }
            }
        }

        foreach (var name in oldDefaults.Keys)
        {
            if (!newDefaults.ContainsKey(name))
            {
                impact.Add(new ImpactItem { Type = "DefaultConstraint", Action = "Dropped", Table = oldEntity.Name, Name = name });
            }
        }
    }
    #endregion

    private static string BuildConstraintDetails(ConstraintDefinition constraint)
    {
        if (constraint == null) return "";

        switch (constraint.Type.ToUpperInvariant())
        {
            case "PRIMARY KEY":
            case "UNIQUE":
                return string.Join(", ", constraint.Columns);

            case "FOREIGN KEY":
                var cols = string.Join(", ", constraint.Columns);
                var refCols = string.Join(", ", constraint.ReferencedColumns);
                return $"{cols} → {constraint.ReferencedTable}({refCols})";

            case "DEFAULT":
                return NormalizeExpression(constraint.DefaultValue);

            default:
                return string.Join(", ", constraint.Columns);
        }
    }

    #region Helpers
    /// <summary>
    /// Normalizes SQL expressions for comparison by trimming, removing extra spaces, and lowering case.
    /// </summary>

    private static string NormalizeExpression(string expr)
    {
        return Regex.Replace(expr ?? "", @"\s+", "").ToLowerInvariant();
    }

    #endregion
}

