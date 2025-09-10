namespace Syn.Core.MultiTenancy.DI
{
    public sealed class TenantLogger : ITenantLogger
    {
        private readonly string _tenantId;

        public TenantLogger(string tenantId)
        {
            _tenantId = tenantId;
        }

        public void Log(string message)
        {
            Console.WriteLine($"[{_tenantId}] {message}");
        }
    }
}
