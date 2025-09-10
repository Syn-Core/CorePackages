using Microsoft.Extensions.DependencyInjection;

using Syn.Core.MultiTenancy.Events;

namespace Syn.Core.MultiTenancy
{
    /// <summary>
    /// Extension methods for registering tenant event infrastructure.
    /// </summary>
    public static class TenantEventsServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the core tenant event infrastructure, including the TenantEventPublisher.
        /// Handlers (ITenantEventHandler) should be registered separately by the application.
        /// </summary>
        /// <param name="services">The service collection.</param>
        public static IServiceCollection AddTenantEvents(this IServiceCollection services)
        {
            // Register the publisher as a singleton so it can be injected anywhere
            services.AddSingleton<TenantEventPublisher>();

            return services;
        }
    }
}
