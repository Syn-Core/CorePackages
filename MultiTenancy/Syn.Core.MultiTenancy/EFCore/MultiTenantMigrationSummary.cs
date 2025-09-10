namespace Syn.Core.MultiTenancy.EFCore
{
    /// <summary>
     /// Aggregated results across multiple tenants.
     /// </summary>
    public sealed class MultiTenantMigrationSummary
    {
        public int TotalTenants { get; init; }
        public int Succeeded { get; init; }
        public int Failed { get; init; }
        public IReadOnlyDictionary<string, MigrationRunReport> Reports { get; init; } = new Dictionary<string, MigrationRunReport>();
        public TimeSpan TotalDuration { get; init; }
    }
}
