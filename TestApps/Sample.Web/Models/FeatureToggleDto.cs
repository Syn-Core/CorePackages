using System.ComponentModel.DataAnnotations;

namespace Sample.Web.Models
{
    /// <summary>
    /// Represents a single feature toggle entry.
    /// </summary>
    public class FeatureToggleDto
    {
        /// <summary>
        /// The feature name.
        /// Example: "BetaFeature"
        /// </summary>
        [Required]
        public string FeatureName { get; set; } = default!;

        /// <summary>
        /// Whether the feature is enabled.
        /// Example: false
        /// </summary>
        public bool IsEnabled { get; set; }
    }
}