using Microsoft.EntityFrameworkCore;

namespace Syn.Core.MultiTenancy.Features.Database;

/// <summary>
/// Tenant-aware feature flag provider backed by EF Core.
/// </summary>
public sealed class EfDatabaseTenantFeatureFlagProvider<TDbContext> : ITenantFeatureFlagProvider
    where TDbContext : DbContext

{
    private readonly TDbContext _db;

    public EfDatabaseTenantFeatureFlagProvider(TDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<string>> GetEnabledFeaturesAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _db.Set<TenantFeatureFlag>()
            .Where(f => f.TenantId == tenantId && f.IsEnabled)
            .Select(f => f.FeatureName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }


    public async Task<bool> IsEnabledAsync(string tenantId, string featureName, CancellationToken cancellationToken = default)
    {
        return await _db.Set<TenantFeatureFlag>()
            .AnyAsync(f => f.TenantId == tenantId && f.FeatureName == featureName && f.IsEnabled, cancellationToken)
            .ConfigureAwait(false);
    }

}
