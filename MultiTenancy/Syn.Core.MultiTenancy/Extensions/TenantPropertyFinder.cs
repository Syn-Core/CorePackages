using Syn.Core.MultiTenancy.Context;

using System.Reflection;

namespace Syn.Core.MultiTenancy.Extensions
{
    /// <summary>
    /// Attempts to find the tenant property using three strategies:
    /// 1. Attribute-based ([TenantId])
    /// 2. Interface-based (ITenantEntity)
    /// 3. Context-based property name
    /// </summary>
    /// <summary>
    /// Utility class for finding the tenant property on an entity type.
    /// Supports attribute-based, interface-based, and context-based resolution.
    /// </summary>
    public static class TenantPropertyFinder
    {
        /// <summary>
        /// Finds the tenant property for the given entity type.
        /// </summary>
        /// <param name="entityType">The entity type to inspect.</param>
        /// <param name="contextPropertyName">
        /// The property name provided by the tenant context (e.g., "TenantId").
        /// </param>
        /// <returns>
        /// The <see cref="PropertyInfo"/> of the tenant property if found; otherwise, <c>null</c>.
        /// </returns>
        public static PropertyInfo? Find(Type entityType, string contextPropertyName)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            // 1️⃣ Attribute-based: [TenantId]
            var attrProp = entityType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => p.GetCustomAttribute<TenantIdAttribute>() != null);
            if (attrProp != null)
                return attrProp;

            // 2️⃣ Interface-based: ITenantEntity
            if (typeof(ITenantEntity).IsAssignableFrom(entityType))
            {
                var interfaceProp = entityType.GetProperty(nameof(ITenantEntity.TenantId));
                if (interfaceProp != null)
                    return interfaceProp;
            }

            // 3️⃣ Context-based: match by property name (case-insensitive)
            var possibleNames = new[]
            {
                MultiTenancyOptions.Instance.DefaultTenantPropertyName,
                nameof(ITenantEntity.TenantId),
                contextPropertyName,
                "TenantId",
                "TenantID",
                "Tenant_Id",
                "TId",
                "T_Id",
            };

            var nameProp = entityType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => possibleNames.Contains(p.Name, StringComparer.OrdinalIgnoreCase));

            return nameProp;
        }
    }


}
