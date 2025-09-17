using Microsoft.AspNetCore.Http;

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
    private readonly HashSet<string> _rootDomains;
    private readonly bool _includeAllSubLevels;
    private readonly HashSet<string> _excludedSubdomains;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubdomainTenantResolutionStrategy"/> class.
    /// </summary>
    /// <param name="rootDomains">List of root domains (e.g., "example.com").</param>
    /// <param name="includeAllSubLevels">If true, returns all subdomain levels before the root domain.</param>
    /// <param name="excludedSubdomains">List of subdomains to ignore (e.g., "www", "app").</param>
    public SubdomainTenantResolutionStrategy(
        IEnumerable<string> rootDomains,
        bool includeAllSubLevels = false,
        IEnumerable<string>? excludedSubdomains = null)
    {
        if (rootDomains == null || !rootDomains.Any())
            throw new ArgumentNullException(nameof(rootDomains));

        _rootDomains = new HashSet<string>(
            rootDomains.Select(d => d.ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);

        _includeAllSubLevels = includeAllSubLevels;

        _excludedSubdomains = new HashSet<string>(
            excludedSubdomains?.Select(s => s.ToLowerInvariant()) ?? [],
            StringComparer.OrdinalIgnoreCase);
    }


    /// <inheritdoc />
    public IEnumerable<string> ResolveTenantIds(object context)
    {
        if (context is not HttpContext httpContext)
            yield break;

        var host = httpContext.Request.Host.Host;

        // Normalize host (remove www.)
        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            host = host[4..];

        // Find matching root domain
        var matchedRoot = _rootDomains.FirstOrDefault(rd =>
            host.EndsWith(rd, StringComparison.OrdinalIgnoreCase));

        if (matchedRoot == null)
            yield break;

        // Extract subdomain part
        var subdomainPart = host[..^matchedRoot.Length].TrimEnd('.');

        if (string.IsNullOrWhiteSpace(subdomainPart))
            yield break;

        // Split into levels
        var parts = subdomainPart.Split('.', StringSplitOptions.RemoveEmptyEntries);

        // Exclude unwanted subdomains
        parts = parts.Where(p => !_excludedSubdomains.Contains(p.ToLowerInvariant())).ToArray();

        if (!parts.Any())
            yield break;

        // Return either first part or all parts joined
        var tenantId = _includeAllSubLevels
            ? string.Join('.', parts)
            : parts.First();

        yield return tenantId.ToLowerInvariant();
    }
}
