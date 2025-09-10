using Syn.Core.MultiTenancy.Metadata;

namespace Syn.Core.MultiTenancy
{
    /// <summary>
    /// Abstraction for retrieving tenant information from storage.
    /// </summary>
    public interface ITenantRepository
    {
        Task<TenantInfo> GetTenantByIdAsync(string tenantId);
        Task<IEnumerable<TenantInfo>> GetTenantsByFlagAsync(string flagKey);
    }
}