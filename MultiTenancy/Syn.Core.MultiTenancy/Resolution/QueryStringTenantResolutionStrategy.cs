using System;

using Microsoft.AspNetCore.Http;

namespace Syn.Core.MultiTenancy.Resolution
{
    /// <summary>
    /// Resolves the tenant identifier from a specific query string parameter in the HTTP request.
    /// </summary>
    /// <remarks>
    /// This strategy assumes that the tenant identifier is passed as a query string parameter.
    /// For example:
    /// <code>
    /// https://example.com/orders?tenantId=tenant1
    /// </code>
    /// If the configured parameter name is "tenantId", the resolved tenant ID will be "tenant1".
    /// </remarks>
    public class QueryStringTenantResolutionStrategy : ITenantResolutionStrategy
    {
        private readonly string _parameterName;

        /// <summary>
        /// Initializes a new instance of the <see cref="QueryStringTenantResolutionStrategy"/> class.
        /// </summary>
        /// <param name="parameterName">
        /// The name of the query string parameter that contains the tenant identifier.
        /// </param>
        public QueryStringTenantResolutionStrategy(string parameterName)
        {
            _parameterName = parameterName ?? throw new ArgumentNullException(nameof(parameterName));
        }

        /// <inheritdoc />
        public IEnumerable<string> ResolveTenantIds(object context)
        {
            if (context is not HttpContext httpContext)
                yield break;
                //throw new ArgumentException("Expected an HttpContext instance.", nameof(context));

            if (httpContext.Request.Query.TryGetValue(_parameterName, out var values))
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