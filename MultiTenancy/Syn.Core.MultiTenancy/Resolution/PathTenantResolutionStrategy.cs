using System;

using Microsoft.AspNetCore.Http;

namespace Syn.Core.MultiTenancy.Resolution;

/// <summary>
/// Resolves the tenant identifier from a specific segment in the request path.
/// </summary>
/// <remarks>
/// This strategy assumes that the tenant identifier is embedded in the URL path at a known segment index.
/// For example:
/// <list type="bullet">
/// <item><description>URL: <c>/t/tenant1/orders</c>, Segment Index: 1 → Tenant ID = "tenant1"</description></item>
/// <item><description>URL: <c>/tenant2/products</c>, Segment Index: 0 → Tenant ID = "tenant2"</description></item>
/// </list>
/// </remarks>
public class PathTenantResolutionStrategy : ITenantResolutionStrategy
{
    private readonly int _segmentIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="PathTenantResolutionStrategy"/> class.
    /// </summary>
    /// <param name="segmentIndex">
    /// The zero-based index of the path segment that contains the tenant identifier.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="segmentIndex"/> is negative.
    /// </exception>
    public PathTenantResolutionStrategy(int segmentIndex)
    {
        if (segmentIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(segmentIndex), "Segment index must be non-negative.");

        _segmentIndex = segmentIndex;
    }

    /// <inheritdoc />
    public IEnumerable<string> ResolveTenantIds(object context)
    {
        if (context is not HttpContext httpContext)
            yield break;
            //throw new ArgumentException("Expected an HttpContext instance.", nameof(context));

        var segments = httpContext.Request.Path.Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments != null && segments.Length > _segmentIndex)
        {
            var tenantId = segments[_segmentIndex]?.Trim();
            if (!string.IsNullOrWhiteSpace(tenantId))
                yield return tenantId;
        }
    }
}