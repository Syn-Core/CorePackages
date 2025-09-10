using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Syn.Core.MultiTenancy;
using Syn.Core.MultiTenancy.Context;
using Syn.Core.MultiTenancy.Extensions;

using System.Reflection;

namespace Syn.Core.MultiTenancy.EFCore
{
    /// <summary>
    /// Stamps TenantId on Added entities and enforces tenant isolation on Update/Delete operations.
    /// Supports multi-tenant context with multiple allowed tenants.
    /// </summary>
    public sealed class TenantSaveChangesInterceptor : SaveChangesInterceptor
    {
        private readonly ITenantContext _tenantContext;

        public TenantSaveChangesInterceptor(ITenantContext tenantContext)
        {
            _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            if (eventData.Context is { } db) EnforceTenantRules(db);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context is { } db) EnforceTenantRules(db);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void EnforceTenantRules(DbContext db)
        {
            var allowedTenantIds = _tenantContext.Tenants
                .Select(t => t.TenantId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);

            if (!allowedTenantIds.Any())
                throw new InvalidOperationException("No tenant context available for this operation.");

            var entries = db.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .ToList();

            foreach (var entry in entries)
            {
                var tenantProp = TenantPropertyFinder.Find(entry.Entity.GetType(), _tenantContext.TenantPropertyName);
                if (tenantProp is null)
                    continue; // Entity is not tenant-scoped

                var currentValue = tenantProp.GetValue(entry.Entity);

                if (entry.State == EntityState.Added)
                {
                    // Auto-stamp if null/default
                    if (currentValue is null || IsDefault(currentValue))
                    {
                        var stampTenantId = _tenantContext.ActiveTenant?.TenantId
                                             ?? allowedTenantIds.First();
                        SetValueRespectingType(tenantProp, entry.Entity, stampTenantId);
                        continue;
                    }
                }

                // For Modified or Deleted, enforce allowed tenants
                if (!IsInAllowedTenants(tenantProp, entry.Entity, allowedTenantIds))
                {
                    throw new InvalidOperationException(
                        $"Cross-tenant operation detected for entity '{entry.Entity.GetType().Name}'.");
                }
            }
        }

        private static bool IsDefault(object value)
        {
            var type = Nullable.GetUnderlyingType(value.GetType()) ?? value.GetType();
            return value.Equals(type.IsValueType ? Activator.CreateInstance(type)! : null);
        }

        private static void SetValueRespectingType(PropertyInfo prop, object entity, string tenantId)
        {
            var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            object converted = targetType == typeof(string)
                ? tenantId
                : Convert.ChangeType(tenantId, targetType, System.Globalization.CultureInfo.InvariantCulture);

            prop.SetValue(entity, converted);
        }

        private static bool IsInAllowedTenants(PropertyInfo prop, object entity, HashSet<string> allowedTenantIds)
        {
            var value = prop.GetValue(entity);
            if (value is null) return false;

            var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if (targetType == typeof(string))
                return allowedTenantIds.Contains((string)value);

            try
            {
                var stringValue = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
                return stringValue != null && allowedTenantIds.Contains(stringValue);
            }
            catch
            {
                return false;
            }
        }
    }
}