namespace Syn.Core.MultiTenancy.Features.Internal
{
    /// <summary>
    /// Internal abstraction for retrieving feature flags from a specific source.
    /// </summary>
    public interface IFeatureFlagSource
    {
        Task<IEnumerable<string>>? GetFlagsAsync(string tenantId, CancellationToken cancellationToken = default);
    }
}
