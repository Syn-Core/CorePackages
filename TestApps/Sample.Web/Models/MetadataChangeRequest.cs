using System.ComponentModel.DataAnnotations;

namespace Sample.Web.Models
{
    /// <summary>
    /// Represents a request to update tenant metadata before publishing the change event.
    /// </summary>
    public class MetadataChangeRequest
    {
        /// <summary>
        /// The metadata key-value pairs to merge into the tenant's metadata.
        /// Example:
        /// {
        ///   "plan": "pro",
        ///   "region": "eu"
        /// }
        /// </summary>
        [Required]
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}