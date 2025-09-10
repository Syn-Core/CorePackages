using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Syn.Core.MultiTenancy.Features.Database;

internal class TenantFeatureFlagEntityConfiguration : IEntityTypeConfiguration<TenantFeatureFlag>
{
    public void Configure(EntityTypeBuilder<TenantFeatureFlag> entity)
    {
        entity.HasKey(f => f.Id);
        entity.HasIndex(f => new { f.TenantId, f.FeatureName }).IsUnique();
        entity.Property(f => f.TenantId).HasMaxLength(100).IsRequired();
        entity.Property(f => f.FeatureName).HasMaxLength(100).IsRequired();
    }
}
