using Microsoft.AspNetCore.Http;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Syn.Core.MultiTenancy.Resolution
{
    /// <summary>
    /// Resolves the tenant identifier from the full domain name of the incoming request.
    /// </summary>
    /// <remarks>
    /// This strategy assumes that each tenant is mapped to a unique domain.
    /// For example:
    /// <list type="bullet">
    /// <item><description><c>tenant1.com</c> → Tenant ID = "tenant1"</description></item>
    /// <item><description><c>tenant2.org</c> → Tenant ID = "tenant2"</description></item>
    /// </list>
    /// </remarks>
    public class DomainTenantResolutionStrategy : ITenantResolutionStrategy
    {
        private readonly Regex _domainPattern;

        /// <summary>
        /// Initializes a new instance of the <see cref="DomainTenantResolutionStrategy"/> class.
        /// </summary>
        /// <param name="pattern">
        /// A regular expression pattern used to extract the tenant identifier from the domain.
        /// Example: <c>^(?<tenant>[^.]+)\.com$</c> will extract "tenant" from "tenant.com".
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pattern"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="pattern"/> is not a valid regex.</exception>
        public DomainTenantResolutionStrategy(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                throw new ArgumentNullException(nameof(pattern));

            try
            {
                _domainPattern = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Invalid regex pattern for domain matching.", nameof(pattern), ex);
            }
        }

        /// <inheritdoc />
        public IEnumerable<string> ResolveTenantIds(object context)
        {
            if (context is not HttpContext httpContext)
                yield break;
            // throw new ArgumentException("Expected an HttpContext instance.", nameof(context));

            var host = httpContext.Request.Host.Host;

            // Normalize host (remove www.)
            if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                host = host[4..];

            var match = _domainPattern.Match(host);
            if (match.Success && match.Groups["tenant"].Success)
            {
                var tenantId = match.Groups["tenant"].Value.Trim();
                if (!string.IsNullOrWhiteSpace(tenantId))
                    yield return tenantId;
            }
        }
    }
}
