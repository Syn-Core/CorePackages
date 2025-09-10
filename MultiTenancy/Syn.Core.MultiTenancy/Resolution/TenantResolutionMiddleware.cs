using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

using Syn.Core.MultiTenancy;
using Syn.Core.MultiTenancy.Context;
using Syn.Core.MultiTenancy.Metadata;

namespace Syn.Core.MultiTenancy.Resolution
{
    /// <summary>
    /// Middleware that resolves one or more tenants for the current request
    /// and populates the <see cref="ITenantContext"/> with their information.
    /// Only active tenants (IsActive = true) are included in the context by default.
    /// </summary>
    /// <remarks>
    /// This middleware should be registered early in the ASP.NET Core request pipeline,
    /// after authentication (if tenant resolution depends on user claims) and before
    /// any middleware or components that require tenant information.
    /// </remarks>
    public class TenantResolutionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ITenantResolutionStrategy _strategy;
        private readonly ITenantContextAccessor _accessor;
        private readonly ITenantStore _tenantStore;
        private readonly MultiTenancyOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="TenantResolutionMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="strategy">The tenant resolution strategy to use.</param>
        /// <param name="accessor">The tenant context accessor to store the resolved tenants.</param>
        /// <param name="tenantStore">The tenant store used to retrieve tenant metadata.</param>
        /// <param name="options">Multi-tenancy configuration options.</param>
        public TenantResolutionMiddleware(
            RequestDelegate next,
            ITenantResolutionStrategy strategy,
            ITenantContextAccessor accessor,
            ITenantStore tenantStore,
            IOptions<MultiTenancyOptions> options)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
            _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
            _tenantStore = tenantStore ?? throw new ArgumentNullException(nameof(tenantStore));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Invokes the middleware to resolve tenants and set the tenant context.
        /// </summary>
        /// <param name="context">The current HTTP context.</param>
        public async Task InvokeAsync(HttpContext context)
        {
            // Resolve tenant IDs from the strategy
            var tenantIds = _strategy.ResolveTenantIds(context)?.ToList() ?? new List<string>();

            var tenants = new List<TenantInfo>();

            foreach (var id in tenantIds)
            {
                // الوضع الافتراضي: لا يشمل الـ Inactive
                var info = await _tenantStore.GetAsync(id, includeInactive: false);

                if (info != null)
                    tenants.Add(info);
            }

            // Create the multi-tenant context
            _accessor.TenantContext = new MultiTenantContext(tenants, _options.DefaultTenantPropertyName);

            // Continue to the next middleware
            await _next(context);
        }
    }


}