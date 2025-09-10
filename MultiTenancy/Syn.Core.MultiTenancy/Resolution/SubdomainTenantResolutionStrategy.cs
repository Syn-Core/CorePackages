using Microsoft.AspNetCore.Http;

using System;

namespace Syn.Core.MultiTenancy.Resolution;

/// <summary>
/// Resolves the tenant identifier from the request's host name using a subdomain-based convention.
/// </summary>
/// <remarks>
/// This strategy assumes that each tenant is mapped to a unique subdomain.
/// For example:
/// <list type="bullet">
/// <item><description><c>tenant1.example.com</c> → Tenant ID = "tenant1"</description></item>
/// <item><description><c>tenant2.example.com</c> → Tenant ID = "tenant2"</description></item>
/// </list>
/// The <paramref name="rootDomain"/> should be provided without protocol or subdomain parts.
/// </remarks>
public class SubdomainTenantResolutionStrategy : ITenantResolutionStrategy
{
    private readonly string _rootDomain;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubdomainTenantResolutionStrategy"/> class.
    /// </summary>
    /// <param name="rootDomain">
    /// The root domain to strip from the host name when extracting the tenant identifier.
    /// Example: "example.com"
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rootDomain"/> is null.</exception>
    public SubdomainTenantResolutionStrategy(string rootDomain)
    {
        _rootDomain = rootDomain ?? throw new ArgumentNullException(nameof(rootDomain));
    }

    /// <inheritdoc />
    public IEnumerable<string> ResolveTenantIds(object context)
    {
        if (context is not HttpContext httpContext)
            throw new ArgumentException("Expected an HttpContext instance.", nameof(context));

        var host = httpContext.Request.Host.Host;

        // Normalize host (remove www.)
        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            host = host[4..];

        if (!host.EndsWith(_rootDomain, StringComparison.OrdinalIgnoreCase))
            yield break;

        var subdomainPart = host[..^_rootDomain.Length].TrimEnd('.');
        if (!string.IsNullOrWhiteSpace(subdomainPart))
            yield return subdomainPart;
    }
}
