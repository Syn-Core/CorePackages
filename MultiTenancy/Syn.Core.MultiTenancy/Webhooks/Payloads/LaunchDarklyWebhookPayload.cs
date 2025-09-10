namespace Syn.Core.MultiTenancy.Webhooks.Payloads
{
    /// <summary>
    /// Payload model for LaunchDarkly webhook events.
    /// </summary>
    public class LaunchDarklyWebhookPayload
    {
        public string FlagKey { get; set; }
        public string Environment { get; set; }
        public object Target { get; set; }
    }
}