using Microsoft.Extensions.DependencyInjection;

namespace Syn.Core.MultiTenancy.DI
{
    /// <summary>
    /// Captures a snapshot (shallow copy) of an <see cref="IServiceCollection"/> to serve as the base
    /// for per-tenant containers.
    /// </summary>
    public interface IServiceCollectionSnapshot
    {
        /// <summary>
        /// Creates a fresh copy of the base services to be used for building a tenant container.
        /// </summary>
        IServiceCollection Clone();
    }
}
