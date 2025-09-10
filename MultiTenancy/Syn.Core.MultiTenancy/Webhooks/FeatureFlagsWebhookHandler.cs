using Syn.Core.MultiTenancy.Events;
using Syn.Core.MultiTenancy.Webhooks.Payloads;

namespace Syn.Core.MultiTenancy.Webhooks
{
    /// <summary>
    /// Handles incoming webhook payloads from LaunchDarkly and Azure App Configuration.
    /// Publishes tenant events to invalidate caches or trigger updates.
    /// </summary>
    public sealed class FeatureFlagsWebhookHandler
    {
        private readonly TenantEventPublisher _publisher;
        private readonly ITenantRepository _tenantRepository;

        public FeatureFlagsWebhookHandler(
            TenantEventPublisher publisher,
            ITenantRepository tenantRepository)
        {
            _publisher = publisher;
            _tenantRepository = tenantRepository;
        }

        public async Task HandleLaunchDarklyAsync(LaunchDarklyWebhookPayload payload)
        {
            if (payload?.FlagKey == null) return;

            var tenants = await _tenantRepository.GetTenantsByFlagAsync(payload.FlagKey);
            foreach (var tenant in tenants)
            {
                await _publisher.PublishMetadataChangedAsync(tenant);
            }
        }

        public async Task HandleAzureAppConfigAsync(AzureAppConfigWebhookPayload payload)
        {
            if (payload?.Key == null) return;

            var parts = payload.Key.Split(':');
            if (parts.Length >= 2)
            {
                var tenantId = parts[1];
                var tenant = await _tenantRepository.GetTenantByIdAsync(tenantId);
                if (tenant != null)
                {
                    await _publisher.PublishMetadataChangedAsync(tenant);
                }
            }
        }
    }
}