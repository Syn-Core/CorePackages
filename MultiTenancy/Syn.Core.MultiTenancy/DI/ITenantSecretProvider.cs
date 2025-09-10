namespace Syn.Core.MultiTenancy.DI
{
    /// <summary>
    /// Abstraction for retrieving secrets for a specific tenant.
    /// </summary>
    public interface ITenantSecretProvider
    {
        Task<string?> GetSecretAsync(string tenantId, string secretName, CancellationToken cancellationToken = default);
    }
}
