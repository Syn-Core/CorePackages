using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

using Syn.Core.MultiTenancy.Metadata;
using Syn.Core.MultiTenancy.Resolvers;

namespace Syn.Core.MultiTenancy
{
    /// <summary>
    /// Represents configuration options for the multi-tenancy system.
    /// </summary>
    /// <remarks>
    /// These options control how the tenant identifier is resolved from incoming requests,
    /// including claim-based, header-based, query string-based, domain-based, and subdomain-based strategies.
    /// 
    /// <para>
    /// Example usage in <c>Program.cs</c>:
    /// <code>
    /// services.AddMultiTenancy(options =>
    /// {
    ///     options.ClaimKey = "tid";
    ///     options.HeaderKey = "X-Tid";
    ///     options.QueryKey = "tid";
    ///     options.DomainRegexPattern = @"^(?&lt;tenant&gt;[^.]+)\.example\.com$";
    ///     options.RootDomains = new[] { "example.com", "example.org" };
    ///     options.IncludeAllSubLevels = false;
    ///     options.ExcludedSubdomains = new[] { "www", "app" };
    /// });
    /// </code>
    /// </para>
    /// </remarks>
    public class MultiTenancyOptions
    {
        /// <summary>
        /// <inheritdoc cref="MultiTenancyOptions" />
        /// </summary>
        internal static MultiTenancyOptions Instance => new ();
        /// <summary>
        /// Default property name to use for tenant filtering if no attribute or interface is found.
        /// </summary>
        public string DefaultTenantPropertyName { get; set; } = "TenantId";

        /// <summary>
        /// Whether to register the default SaveChanges interceptor for tenant enforcement.
        /// </summary>
        public bool UseTenantInterceptor { get; set; } = true;

        /// <summary>
        /// The claim type key used to resolve the tenant identifier from the authenticated user's claims.
        /// </summary>
        /// <remarks>
        /// Default value is <c>"tenant_id"</c>.
        /// Example: If the user's claims contain <c>tenant_id = "tenant1"</c>, the tenant will be resolved as "tenant1".
        /// </remarks>
        public string? ClaimKey { get; set; } = "tenant_id";

        /// <summary>
        /// The HTTP header name used to resolve the tenant identifier from incoming requests.
        /// </summary>
        /// <remarks>
        /// Default value is <c>"X-Tenant-ID"</c>.
        /// Example: If the request contains header <c>X-Tenant-ID: tenant1</c>, the tenant will be resolved as "tenant1".
        /// </remarks>
        public string? HeaderKey { get; set; } = "X-Tenant-ID";

        /// <summary>
        /// The query string parameter name used to resolve the tenant identifier from the request URL.
        /// </summary>
        /// <remarks>
        /// Default value is <c>"tenantId"</c>.
        /// Example: <c>https://app.example.com?tenantId=tenant1</c> will resolve the tenant as "tenant1".
        /// </remarks>
        public string? QueryKey { get; set; } = "tenantId";

        /// <summary>
        /// A regular expression pattern used to extract the tenant identifier from the request's domain name.
        /// </summary>
        /// <remarks>
        /// Example: <c>^(?<tenant>[^.]+)\.example\.com$</c> will extract "tenant1" from "tenant1.example.com".
        /// </remarks>
        public string? DomainRegexPattern { get; set; }

        /// <summary>
        /// A collection of root domains used for subdomain-based tenant resolution.
        /// </summary>
        /// <remarks>
        /// Example: <c>["example.com", "example.org"]</c>.
        /// If the request host is "tenant1.example.com", the tenant will be resolved as "tenant1".
        /// </remarks>
        public IEnumerable<string>? RootDomains { get; set; }

        /// <summary>
        /// Determines whether all subdomain levels before the root domain should be included in the resolved tenant identifier.
        /// </summary>
        /// <remarks>
        /// Default is <c>false</c>.
        /// If <c>true</c> and the host is "branch1.tenant1.example.com", the resolved tenant will be "branch1.tenant1".
        /// If <c>false</c>, only "branch1" will be used.
        /// </remarks>
        public bool IncludeAllSubLevels { get; set; } = false;

        /// <summary>
        /// A collection of subdomain names to exclude from tenant resolution.
        /// </summary>
        /// <remarks>
        /// Example: <c>["www", "app", "admin"]</c>.
        /// If the host is "www.example.com", no tenant will be resolved.
        /// </remarks>
        public IEnumerable<string>? ExcludedSubdomains { get; set; }






        /// <summary>
        /// Factory method for creating the ITenantStore implementation.
        /// Defaults to Cached InMemoryTenantStore for development.
        /// </summary>
        public Func<IServiceProvider, ITenantStore> TenantStoreFactory { get; set; }
            = sp =>
            {
                var memoryCache = sp.GetRequiredService<IMemoryCache>();

                // In-memory store with empty initial list
                var innerStore = new InMemoryTenantStore(new List<TenantInfo>());

                // Wrap with caching
                return new CachedTenantStore(innerStore, memoryCache);
            };

        /// <summary>
        /// Factory method for creating the ITenantIdProvider implementation.
        /// Defaults to DefaultTenantIdProvider (throws until implemented).
        /// </summary>
        public Func<IServiceProvider, ITenantIdProvider> TenantIdProviderFactory { get; set; }
            = sp => new DefaultTenantIdProvider();
    }
}