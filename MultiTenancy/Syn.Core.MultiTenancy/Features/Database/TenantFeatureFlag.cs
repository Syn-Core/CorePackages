using Microsoft.EntityFrameworkCore;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Syn.Core.MultiTenancy.Features.Database;

/// <summary>
/// Represents a feature flag assigned to a specific tenant.
/// </summary>
[Table("TenantFeatureFlags")]
[Index(nameof(Description), IsUnique = false)]
public class TenantFeatureFlag
{
    [Key]
    [MaxLength(450)] // منع Auto-Fix وتحديد الطول من البداية
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(100)]
    public string TenantId { get; set; }

    [Required]
    [MaxLength(100)]
    public string FeatureName { get; set; }

    public bool IsEnabled { get; set; }

    // عمود جديد لاختبار المايجريشن
    [MaxLength(450)]
    public string? Description { get; set; }
}

