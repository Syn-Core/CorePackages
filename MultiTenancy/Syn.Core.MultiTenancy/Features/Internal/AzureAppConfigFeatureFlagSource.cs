using Azure;
using Azure.Data.AppConfiguration;

namespace Syn.Core.MultiTenancy.Features.Internal
{
    /// <summary>
    /// Low-level data source for retrieving feature flags from Azure App Configuration.
    /// Key format expected: FeatureManagement:{tenantId}:{featureName} = true|false
    /// </summary>
    internal sealed class AzureAppConfigFeatureFlagSource : IFeatureFlagSource
    {
        private readonly ConfigurationClient _client;

        /// <summary>
        /// Initializes a new instance with the given Azure App Configuration connection string.
        /// </summary>
        public AzureAppConfigFeatureFlagSource(string connectionString)
        {
            _client = new ConfigurationClient(connectionString);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<string>> GetFlagsAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            // For SDK versions where SettingSelector has no ctor params, use property initializer.
            var selector = new SettingSelector
            {
                KeyFilter = $"FeatureManagement:{tenantId}:*",
                // LabelFilter is optional; set if you use labels per environment/plan.
                // LabelFilter = "prod"
            };

            var settings = _client.GetConfigurationSettingsAsync(selector, cancellationToken: cancellationToken);

            var enabled = new List<string>();
            await foreach (ConfigurationSetting setting in settings.ConfigureAwait(false))
            {
                // Example key: FeatureManagement:tenant1:FeatureA
                var parts = setting.Key.Split(':');
                if (parts.Length >= 3)
                {
                    var featureName = parts[^1]; // last segment
                    if (bool.TryParse(setting.Value, out var isEnabled) && isEnabled)
                        enabled.Add(featureName);
                }
            }

            return enabled;
        }

        /// <summary>
        /// Attempts to read a single feature flag value for a tenant.
        /// </summary>
        public async Task<bool?> TryGetFlagAsync(string tenantId, string featureName, CancellationToken cancellationToken = default)
        {
            var key = $"FeatureManagement:{tenantId}:{featureName}";
            try
            {
                Response<ConfigurationSetting> response =
                    await _client.GetConfigurationSettingAsync(key, label: null, cancellationToken).ConfigureAwait(false);

                if (response?.Value is ConfigurationSetting cs &&
                    bool.TryParse(cs.Value, out var enabled))
                    return enabled;

                return null;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }
    }
}
