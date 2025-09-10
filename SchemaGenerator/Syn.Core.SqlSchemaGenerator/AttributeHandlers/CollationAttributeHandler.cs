using Syn.Core.SqlSchemaGenerator.Attributes;
using Syn.Core.SqlSchemaGenerator.Models;

using System;
using System.Reflection;

namespace Syn.Core.SqlSchemaGenerator.AttributeHandlers
{
    /// <summary>
    /// Applies <see cref="CollationAttribute"/> values to the column model.
    /// </summary>
    public class CollationAttributeHandler : ISchemaAttributeHandler
    {
        public void Apply(PropertyInfo property, ColumnModel column)
        {
            if (property == null) throw new ArgumentNullException(nameof(property));
            if (column == null) throw new ArgumentNullException(nameof(column));

            var attr = property.GetCustomAttribute<CollationAttribute>();
            if (attr != null)
                column.Collation = attr.Name;
        }
    }
}
