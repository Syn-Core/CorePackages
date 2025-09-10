using Microsoft.EntityFrameworkCore;

namespace Syn.Core.MultiTenancy.EFCore
{
    /// <summary>
    /// Simple stub impact analyzer for demonstration. Replace with your integration.
    /// </summary>
    public sealed class NoopImpactAnalyzer : ISchemaImpactAnalyzer
    {
        public Task<string> AnalyzeAsync(DbContext context, CancellationToken cancellationToken = default)
        {
            // In real life, generate a diff/HTML report using your schema tool.
            return Task.FromResult("Impact analysis is not configured. No-op result.");
        }
    }
}
