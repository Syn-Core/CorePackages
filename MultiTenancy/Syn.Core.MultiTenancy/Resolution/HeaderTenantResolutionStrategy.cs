using System;

using Microsoft.AspNetCore.Http;

namespace Syn.Core.MultiTenancy.Resolution
{
    /// <summary>
    /// Resolves the tenant identifier from a specific HTTP request header.
    /// </summary>
    /// <remarks>
    /// This strategy assumes that the tenant identifier is passed in a known header key.
    /// For example, if the header <c>X-Tenant-ID</c> contains "tenant1", the resolved tenant ID will be "tenant1".
    /// </remarks>
    public class HeaderTenantResolutionStrategy : ITenantResolutionStrategy
    {
        private readonly string _headerName;

        /// <summary>
        /// Initializes a new instance of the <see cref="HeaderTenantResolutionStrategy"/> class.
        /// </summary>
        /// <param name="headerName">
        /// The name of the HTTP header that contains the tenant identifier.
        /// </param>
        public HeaderTenantResolutionStrategy(string headerName)
        {
            _headerName = headerName ?? throw new ArgumentNullException(nameof(headerName));
        }

        /// <inheritdoc />
        public IEnumerable<string> ResolveTenantIds(object context)
        {
            if (context is not HttpContext httpContext)
                throw new ArgumentException("Expected an HttpContext instance.", nameof(context));

            if (httpContext.Request.Headers.TryGetValue(_headerName, out var values))
            {
                foreach (var value in values)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                        yield return value.Trim();
                }
            }
        }

    }
}