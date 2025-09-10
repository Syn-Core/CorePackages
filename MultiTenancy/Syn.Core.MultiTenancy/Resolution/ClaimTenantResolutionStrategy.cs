using System;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace Syn.Core.MultiTenancy.Resolution
{
    /// <summary>
    /// Resolves the tenant identifier from one or more claims in the authenticated user's principal.
    /// </summary>
    /// <remarks>
    /// This strategy checks the specified claim types in order and returns the first non-empty value found.
    /// It requires that the authentication middleware has already populated HttpContext.User.
    /// </remarks>
    public class ClaimTenantResolutionStrategy : ITenantResolutionStrategy
    {
        private readonly string[] _claimTypes;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClaimTenantResolutionStrategy"/> class.
        /// </summary>
        /// <param name="claimTypes">
        /// One or more claim types to check for the tenant identifier, in priority order.
        /// </param>
        public ClaimTenantResolutionStrategy(params string[] claimTypes)
        {
            if (claimTypes == null || claimTypes.Length == 0)
                throw new ArgumentException("At least one claim type must be specified.", nameof(claimTypes));

            _claimTypes = claimTypes;
        }

        /// <inheritdoc />
        public IEnumerable<string> ResolveTenantIds(object context)
        {
            if (context is not HttpContext httpContext)
                throw new ArgumentException("Expected an HttpContext instance.", nameof(context));

            var user = httpContext.User;
            if (user?.Identity?.IsAuthenticated != true)
                yield break;

            foreach (var claimType in _claimTypes)
            {
                var claimValues = user.Claims
                    .Where(c => c.Type.Equals(claimType, StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.Value)
                    .Where(v => !string.IsNullOrWhiteSpace(v));

                foreach (var value in claimValues)
                    yield return value.Trim();
            }
        }

    }
}