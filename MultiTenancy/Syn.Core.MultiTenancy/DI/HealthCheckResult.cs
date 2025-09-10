namespace Syn.Core.MultiTenancy.DI
{
    public sealed class HealthCheckResult
    {
        public bool IsHealthy { get; }
        public string? Message { get; }

        public HealthCheckResult(bool isHealthy, string? message = null)
        {
            IsHealthy = isHealthy;
            Message = message;
        }
    }
}
