namespace Syn.Core.MultiTenancy.EFCore
{
    /// <summary>
    /// Represents the outcome of migrations and/or impact analysis for a single run.
    /// </summary>
    public sealed class MigrationRunReport
    {
        public bool ExecutedMigrations { get; init; }
        public bool PerformedImpactAnalysis { get; init; }
        public string? ImpactSummary { get; init; }
        public Exception? Error { get; init; }
        public TimeSpan Duration { get; init; }
    }
}
