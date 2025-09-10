using System;
using System.Collections.Generic;
using System.Linq;

namespace Syn.Core.MultiTenancy.Resolution
{
    /// <summary>
    /// A composite implementation of <see cref="ITenantResolutionStrategy"/> that
    /// executes multiple strategies in sequence until one or more tenant identifiers are found.
    /// </summary>
    /// <remarks>
    /// This strategy is useful when you want to support multiple ways of resolving tenants
    /// (e.g., from claims, headers, query strings, paths, subdomains) and apply them in a specific priority order.
    /// <para>
    /// The first strategy that returns one or more tenant identifiers will stop further execution
    /// unless <see cref="continueOnMatch"/> is set to <c>true</c>, in which case results from all strategies
    /// will be aggregated.
    /// </para>
    /// </remarks>
    public class CompositeTenantResolutionStrategy : ITenantResolutionStrategy
    {
        private readonly IReadOnlyList<ITenantResolutionStrategy> _strategies;
        private readonly bool _continueOnMatch;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeTenantResolutionStrategy"/> class.
        /// </summary>
        /// <param name="strategies">
        /// The ordered collection of strategies to execute.
        /// </param>
        /// <param name="continueOnMatch">
        /// If <c>true</c>, all strategies will be executed and their results aggregated.
        /// If <c>false</c>, execution stops at the first strategy that returns one or more tenant identifiers.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="strategies"/> is null.
        /// </exception>
        public CompositeTenantResolutionStrategy(
            IEnumerable<ITenantResolutionStrategy> strategies,
            bool continueOnMatch = false)
        {
            if (strategies == null)
                throw new ArgumentNullException(nameof(strategies));

            _strategies = strategies.ToList();
            _continueOnMatch = continueOnMatch;
        }

        /// <inheritdoc />
        public IEnumerable<string> ResolveTenantIds(object context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var resolvedTenantIds = new List<string>();

            foreach (var strategy in _strategies)
            {
                var ids = strategy.ResolveTenantIds(context)
                                  ?.Where(id => !string.IsNullOrWhiteSpace(id))
                                  .Select(id => id.Trim())
                                  .ToList() ?? new List<string>();

                if (ids.Any())
                {
                    resolvedTenantIds.AddRange(ids);

                    if (!_continueOnMatch)
                        break;
                }
            }

            // Remove duplicates while preserving order
            return resolvedTenantIds
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}