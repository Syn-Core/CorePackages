using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

using Syn.Core.MultiTenancy.Metadata;
using Syn.Core.MultiTenancy.Resolvers;

namespace Syn.Core.MultiTenancy
{
    /// <summary>
    /// Options for configuring multi-tenancy services.
    /// </summary>
    public class MultiTenancyOptions
    {
        internal static MultiTenancyOptions Instance => new MultiTenancyOptions();
        /// <summary>
        /// Default property name to use for tenant filtering if no attribute or interface is found.
        /// </summary>
        public string DefaultTenantPropertyName { get; set; } = "TenantId";

        /// <summary>
        /// Whether to register the default SaveChanges interceptor for tenant enforcement.
        /// </summary>
        public bool UseTenantInterceptor { get; set; } = true;

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