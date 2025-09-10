using System.ComponentModel.DataAnnotations;

namespace Sample.Web.Models
{
    /// <summary>
    /// Represents a request to upsert multiple feature flags at once.
    /// </summary>
    public class BulkUpsertFeatureFlagsRequest
    {
        /// <summary>
        /// The list of feature toggles to upsert.
        /// Example:
        /// [
        ///   { "featureName": "NewDashboard", "isEnabled": true },
        ///   { "featureName": "BetaFeature",   "isEnabled": false }
        /// ]
        /// </summary>
        [Required]
        public List<FeatureToggleDto> Items { get; set; } = new();
    }
}