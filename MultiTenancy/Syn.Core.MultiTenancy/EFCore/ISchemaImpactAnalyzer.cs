using Microsoft.EntityFrameworkCore;

namespace Syn.Core.MultiTenancy.EFCore
{
    /// <summary>
    /// Optional analyzer to produce database impact details without executing migrations.
    /// Implementations may integrate with schema diff tools.
    /// </summary>
    public interface ISchemaImpactAnalyzer
    {
        /// <summary>
        /// Produces a human-readable impact summary for the context's pending changes.
        /// </summary>
        Task<string> AnalyzeAsync(DbContext context, CancellationToken cancellationToken = default);
    }
}
