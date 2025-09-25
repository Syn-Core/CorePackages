namespace Syn.Core.SqlSchemaGenerator.Models
{
    /// <summary>
    /// Represents a primary key or foreign key constraint definition.
    /// </summary>
    public class ConstraintDefinition
    {
        /// <summary>
        /// Constraint name in the database.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Constraint type (PRIMARY KEY, FOREIGN KEY, UNIQUE, DEFAULT, etc.).
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Ordered list of columns included in the constraint.
        /// </summary>
        public List<string> Columns { get; set; } = new();

        /// <summary>
        /// For FOREIGN KEY: referenced table name.
        /// </summary>
        public string? ReferencedTable { get; set; }

        /// <summary>
        /// Schema of the referenced table that this foreign key points to. Defaults to "dbo" if not specified.
        /// </summary>
        public string ReferencedSchema { get; set; } = "dbo";

        /// <summary>
        /// For FOREIGN KEY: referenced columns.
        /// </summary>
        public List<string> ReferencedColumns { get; set; } = new();

        /// <summary>
        /// For DEFAULT constraints: the default value expression.
        /// </summary>
        public string? DefaultValue { get; set; }

        public ReferentialAction OnDelete { get; set; } = ReferentialAction.NoAction;
        public ReferentialAction OnUpdate { get; set; } = ReferentialAction.NoAction;

        /// <summary>
        ///  Optional description for the constraint (for documentation / MS_Description).
        /// </summary>
        public string? Description { get; set; }
    }
}