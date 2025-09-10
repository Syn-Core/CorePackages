using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Syn.Core.MultiTenancy.Features.Database;

internal class TenantFeatureFlagModelCustomizer : ModelCustomizer
{
    public TenantFeatureFlagModelCustomizer(ModelCustomizerDependencies dependencies)
        : base(dependencies) { }

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        // إضافة الـ Entity TenantFeatureFlag أوتوماتيك
        modelBuilder.ApplyConfiguration(new TenantFeatureFlagEntityConfiguration());
    }
}
