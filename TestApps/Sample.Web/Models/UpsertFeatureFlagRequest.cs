using System.ComponentModel.DataAnnotations;

namespace Sample.Web.Models
{
    /// <summary>
    /// Represents a request to upsert (add or update) a single feature flag.
    /// </summary>
    public class UpsertFeatureFlagRequest
    {
        /// <summary>
        /// The feature name.
        /// Example: "NewDashboard"
        /// </summary>
        [Required]
        public string FeatureName { get; set; } = default!;

        /// <summary>
        /// Whether the feature is enabled.
        /// Example: true
        /// </summary>
        public bool IsEnabled { get; set; }
    }
}