using LaunchDarkly.Sdk;
using LaunchDarkly.Sdk.Server;


namespace Syn.Core.MultiTenancy.Features.Internal
{
    internal sealed class LaunchDarklyFeatureFlagSource : IFeatureFlagSource, IDisposable
    {
        private readonly LdClient _client;

        public LaunchDarklyFeatureFlagSource(string sdkKey)
        {
            _client = new LdClient(sdkKey);
        }

        public Task<IEnumerable<string>> GetFlagsAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            var context = LaunchDarkly.Sdk.Context.Builder(tenantId)
                .Kind("tenant")
                .Build();

            // Get all flags for this tenant context
            var allFlagsState = _client.AllFlagsState(context);

            // Convert to dictionary of <string, LdValue>
            var allFlags = allFlagsState.ToValuesJsonMap();

            // Filter only boolean flags that are true
            var enabledFlags = allFlags
                .Where(kv => kv.Value.Type == LdValueType.Bool && kv.Value.AsBool)
                .Select(kv => kv.Key);

            return Task.FromResult<IEnumerable<string>>(enabledFlags);
        }

        public void Dispose() => _client.Dispose();
    }
}
