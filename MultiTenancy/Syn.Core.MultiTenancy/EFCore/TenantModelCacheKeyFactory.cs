using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Syn.Core.MultiTenancy.Context;

namespace Syn.Core.MultiTenancy.EFCore;

/// <summary>
/// A generic ModelCacheKeyFactory that includes the current tenant's schema (or ID)
/// in the EF Core model cache key, enabling schema-per-tenant scenarios.
/// Works with any DbContext that can resolve ITenantContext from its service provider.
/// </summary>
public class TenantModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        // نحاول نجيب الـ ITenantContext من الـ DbContext Services
        var tenantContext = context.GetService<ITenantContext>();

        // لو مفيش TenantContext → نستخدم قيمة افتراضية
        var schema = tenantContext?.ActiveTenant?.SchemaName ?? "dbo";

        // نضيف الـ Schema لمفتاح الكاش
        return (context.GetType(), schema, designTime);
    }
}

