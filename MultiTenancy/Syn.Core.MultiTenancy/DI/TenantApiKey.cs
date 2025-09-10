namespace Syn.Core.MultiTenancy.DI
{
    public sealed class TenantApiKey
    {
        public TenantApiKey(string value) => Value = value;
        public string Value { get; }
    }
}
