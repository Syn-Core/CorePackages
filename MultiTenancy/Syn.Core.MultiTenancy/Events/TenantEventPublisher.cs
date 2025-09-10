using Syn.Core.MultiTenancy.Metadata;

namespace Syn.Core.MultiTenancy.Events
{
    /// <summary>
    /// Publishes tenant lifecycle events to all registered handlers.
    /// </summary>
    public sealed class TenantEventPublisher
    {
        private readonly IEnumerable<ITenantEventHandler> _handlers;

        /// <summary>
        /// Initializes a new instance of the publisher with all registered handlers.
        /// </summary>
        public TenantEventPublisher(IEnumerable<ITenantEventHandler> handlers)
        {
            _handlers = handlers;
        }

        public async Task PublishCreatedAsync(TenantInfo tenant, CancellationToken cancellationToken = default)
        {
            foreach (var handler in _handlers)
                await handler.OnTenantCreatedAsync(tenant, cancellationToken).ConfigureAwait(false);
        }

        public async Task PublishActivatedAsync(TenantInfo tenant, CancellationToken cancellationToken = default)
        {
            foreach (var handler in _handlers)
                await handler.OnTenantActivatedAsync(tenant, cancellationToken).ConfigureAwait(false);
        }

        public async Task PublishDeactivatedAsync(TenantInfo tenant, CancellationToken cancellationToken = default)
        {
            foreach (var handler in _handlers)
                await handler.OnTenantDeactivatedAsync(tenant, cancellationToken).ConfigureAwait(false);
        }

        public async Task PublishDeletedAsync(TenantInfo tenant, CancellationToken cancellationToken = default)
        {
            foreach (var handler in _handlers)
                await handler.OnTenantDeletedAsync(tenant, cancellationToken).ConfigureAwait(false);
        }

        public async Task PublishMetadataChangedAsync(TenantInfo tenant, CancellationToken cancellationToken = default)
        {
            foreach (var handler in _handlers)
                await handler.OnTenantMetadataChangedAsync(tenant, cancellationToken).ConfigureAwait(false);
        }
    }
}
