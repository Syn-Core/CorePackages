using Microsoft.EntityFrameworkCore;

using Syn.Core.MultiTenancy;
using Syn.Core.MultiTenancy.Context;
using Syn.Core.MultiTenancy.Extensions;
using Syn.Core.SqlSchemaGenerator.Extensions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Syn.Core.MultiTenancy.EFCore
{
    /// <summary>
    /// Provides extension methods for applying entity definitions to EF Core's ModelBuilder
    /// with multi-tenancy support.
    /// 
    /// This method supports three strategies for identifying the tenant property:
    /// 1. Attribute-based: A property decorated with [TenantId].
    /// 2. Interface-based: An entity implementing ITenantEntity.
    /// 3. Context-based: A property name provided by ITenantContext.TenantPropertyName.
    /// 
    /// Priority: Attribute → Interface → Context property name.
    /// </summary>
    public static class ModelBuilderMultiTenantExtensions
    {
        /// <summary>
        /// Applies entity definitions to the EF Core model with multi-tenancy query filters.
        /// </summary>
        /// <param name="builder">The EF Core model builder.</param>
        /// <param name="entityTypes">The entity types to configure.</param>
        /// <param name="tenantContext">The current tenant context.</param>
        public static void ApplyEntityDefinitionsToModelMultiTenant(
            this ModelBuilder builder,
            IEnumerable<Type> entityTypes,
            ITenantContext tenantContext)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (entityTypes == null) throw new ArgumentNullException(nameof(entityTypes));
            if (tenantContext == null) throw new ArgumentNullException(nameof(tenantContext));

            // 🏷 Schema per Tenant (if single active tenant with its own schema)
            if (!string.IsNullOrWhiteSpace(tenantContext.ActiveTenant?.TenantId))
                builder.HasDefaultSchema(tenantContext.ActiveTenant?.SchemaName);

            // 🏷 Shared Schema: Add global query filter for TenantId(s)
            var tenantIds = tenantContext.Tenants
                .Select(t => t.TenantId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();

            foreach (var type in entityTypes)
            {
                var tenantProp = TenantPropertyFinder.Find(type, tenantContext.TenantPropertyName);

                if (tenantProp != null && tenantIds.Any())
                {
                    var parameter = Expression.Parameter(type, "e");
                    var property = Expression.Property(parameter, tenantProp);

                    Expression body;

                    if (tenantIds.Count == 1)
                    {
                        // e => e.TenantId == singleTenantId
                        var constant = Expression.Constant(tenantIds.First());
                        body = Expression.Equal(property, constant);
                    }
                    else
                    {
                        // e => tenantIds.Contains(e.TenantId)
                        var containsMethod = typeof(List<string>).GetMethod("Contains", new[] { typeof(string) });
                        var constantList = Expression.Constant(tenantIds);
                        body = Expression.Call(constantList, containsMethod!, property);
                    }

                    var lambda = Expression.Lambda(body, parameter);
                    builder.Entity(type).HasQueryFilter(lambda);
                }
            }

            // ✅ Call the core method from SqlSchemaGenerator
            builder.ApplyEntityDefinitionsToModel(entityTypes);
        }
    }
}