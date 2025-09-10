using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;


namespace Syn.Core.MultiTenancy.DI
{
    /// <summary>
    /// Default implementation that clones service descriptors into a new collection.
    /// </summary>
    public sealed class ServiceCollectionSnapshot : IServiceCollectionSnapshot
    {
        private readonly List<ServiceDescriptor> _descriptors;

        /// <summary>
        /// Initializes a new instance by copying the provided service collection.
        /// </summary>
        public ServiceCollectionSnapshot(IServiceCollection baseServices)
        {
            _descriptors = [.. baseServices];
        }

        /// <inheritdoc />
        public IServiceCollection Clone()
        {
            var clone = new ServiceCollection();
            foreach (var d in _descriptors)
                clone.Add(d);
            return clone;
        }
    }
}
