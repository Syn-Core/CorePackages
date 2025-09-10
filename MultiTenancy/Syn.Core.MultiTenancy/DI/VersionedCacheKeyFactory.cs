namespace Syn.Core.MultiTenancy.DI
{
    /// <summary>
    /// Generates cache keys for tenant providers with a version prefix to allow global invalidation.
    /// </summary>
    public sealed class VersionedCacheKeyFactory
    {
        private int _version = 1;

        public string CreateKey(string tenantId) => $"tenant-sp:v{_version}:{tenantId}";

        public void IncrementVersion() => Interlocked.Increment(ref _version);
    }
}
