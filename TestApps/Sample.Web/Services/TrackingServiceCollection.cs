using Sample.Web.Data;

using System.Collections;

public static class ServiceCollectionTrackingExtensions
{
    public static IServiceCollection TrackAppDbContextRegistrations(this IServiceCollection services)
    {
        return new TrackingCollection(services);
    }

    private class TrackingCollection : IServiceCollection
    {
        private readonly IServiceCollection _inner;

        public TrackingCollection(IServiceCollection inner) => _inner = inner;

        public ServiceDescriptor this[int index]
        {
            get => _inner[index];
            set
            {
                LogIfAppDbContext(value);
                _inner[index] = value;
            }
        }

        public int Count => _inner.Count;
        public bool IsReadOnly => _inner.IsReadOnly;

        public void Add(ServiceDescriptor item)
        {
            LogIfAppDbContext(item);
            _inner.Add(item);
        }

        private void LogIfAppDbContext(ServiceDescriptor sd)
        {
            if (sd.ServiceType == typeof(AppDbContext) || sd.ImplementationType == typeof(AppDbContext))
            {
                Console.WriteLine("=== AppDbContext registered here ===");
                Console.WriteLine(Environment.StackTrace);
            }
        }

        // باقي الـ interface delegation
        public void Clear() => _inner.Clear();
        public bool Contains(ServiceDescriptor item) => _inner.Contains(item);
        public void CopyTo(ServiceDescriptor[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);
        public IEnumerator<ServiceDescriptor> GetEnumerator() => _inner.GetEnumerator();
        public int IndexOf(ServiceDescriptor item) => _inner.IndexOf(item);
        public void Insert(int index, ServiceDescriptor item) => _inner.Insert(index, item);
        public bool Remove(ServiceDescriptor item) => _inner.Remove(item);
        public void RemoveAt(int index) => _inner.RemoveAt(index);
        IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();
    }
}