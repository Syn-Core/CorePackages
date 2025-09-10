namespace Syn.Core.MultiTenancy.EFCore
{
    /// <summary>
    /// Abstracts migration (and optional impact analysis) execution against a single database.
    /// </summary>
    public interface IMigrationRunner
    {
        /// <summary>
        /// Runs migrations and/or impact analysis for the provided entity types.
        /// </summary>
        /// <param name="entityTypes">Entity types covered by the model.</param>
        /// <param name="executeMigrations">When true, applies migrations to the database.</param>
        /// <param name="performImpactAnalysis">When true, runs impact analysis and returns a report.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A <see cref="MigrationRunReport"/> describing outcomes.</returns>
        Task<MigrationRunReport> InitiateAsync(
            IEnumerable<Type> entityTypes,
            bool executeMigrations,
            bool performImpactAnalysis,
            CancellationToken cancellationToken = default);
    }
}
