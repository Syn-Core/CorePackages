namespace Syn.Core.MultiTenancy.Webhooks.Payloads
{
    /// <summary>
    /// Payload model for Azure App Configuration webhook events.
    /// </summary>
    public class AzureAppConfigWebhookPayload
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public string ETag { get; set; }
    }
}
