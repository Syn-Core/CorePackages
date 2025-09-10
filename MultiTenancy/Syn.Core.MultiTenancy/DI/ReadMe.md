# 🧩 Tenant-specific DI Container

## 📌 Overview
The **Tenant-specific DI Container** pattern allows each tenant to have its own `IServiceProvider` instance with:
- Custom configurations (SMTP, API Keys, Feature Flags…)
- Tenant-specific services
- Extensibility for future features without changing the core code

---

## 1️⃣ Core Contracts

### `ITenantScopedServiceProviderFactory`
/// <summary>
/// Provides a tenant-specific service provider for resolving services per tenant.
/// </summary>
public interface ITenantScopedServiceProviderFactory
{
    IServiceProvider GetProviderForTenant(string tenantId);
    void Invalidate(string tenantId);
    void InvalidateAll();
}

ITenantServiceConfigurator
/// <summary>
/// Configures tenant-specific service registrations during container creation.
/// </summary>
public interface ITenantServiceConfigurator
{
    void Configure(IServiceCollection services, TenantInfo tenant);
}
IAsyncTenantServiceConfigurator

/// <summary>
/// Asynchronous variant for tenant-specific service configuration.
/// </summary>
public interface IAsyncTenantServiceConfigurator
{
    Task ConfigureAsync(IServiceCollection services, TenantInfo tenant, CancellationToken cancellationToken = default);
}
2️⃣ Options
/// <summary>
/// Options controlling tenant container creation, caching, and behavior.
/// </summary>
public sealed class TenantContainerOptions
{
    public TimeSpan SlidingExpiration { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
    public bool DisposeOnInvalidate { get; set; } = true;
    public Action<string, IServiceProvider>? OnProviderBuilt { get; set; }
    public Func<string, string> CacheKeyFactory { get; set; } = tenantId => $"tenant-sp:{tenantId}";
}
3️⃣ Factory Implementation

/// <summary>
/// Builds and caches tenant-specific service providers from a base service collection,
/// applying both synchronous and asynchronous configurators per tenant.
/// </summary>
public sealed class TenantScopedServiceProviderFactory : ITenantScopedServiceProviderFactory
{
    private readonly IServiceCollection _baseServices;
    private readonly IMemoryCache _cache;
    private readonly TenantContainerOptions _options;
    private readonly ITenantStore _tenantStore;
    private readonly IEnumerable<ITenantServiceConfigurator> _syncConfigurators;
    private readonly IEnumerable<IAsyncTenantServiceConfigurator> _asyncConfigurators;

    public TenantScopedServiceProviderFactory(
        IServiceCollection baseServices,
        IMemoryCache cache,
        IOptions<TenantContainerOptions> options,
        ITenantStore tenantStore,
        IEnumerable<ITenantServiceConfigurator> syncConfigurators,
        IEnumerable<IAsyncTenantServiceConfigurator> asyncConfigurators)
    {
        _baseServices = baseServices;
        _cache = cache;
        _options = options.Value;
        _tenantStore = tenantStore;
        _syncConfigurators = syncConfigurators;
        _asyncConfigurators = asyncConfigurators;
    }

    public IServiceProvider GetProviderForTenant(string tenantId)
    {
        return _cache.GetOrCreate(_options.CacheKeyFactory(tenantId), entry =>
        {
            entry.SlidingExpiration = _options.SlidingExpiration;
            if (_options.AbsoluteExpirationRelativeToNow.HasValue)
                entry.AbsoluteExpirationRelativeToNow = _options.AbsoluteExpirationRelativeToNow;

            var tenant = _tenantStore.GetById(tenantId) ?? throw new InvalidOperationException($"Tenant '{tenantId}' not found.");

            var services = new ServiceCollection();
            foreach (var s in _baseServices)
                services.Add(s);

            foreach (var cfg in _syncConfigurators)
                cfg.Configure(services, tenant);

            foreach (var cfg in _asyncConfigurators)
                cfg.ConfigureAsync(services, tenant).GetAwaiter().GetResult();

            var provider = services.BuildServiceProvider();
            _options.OnProviderBuilt?.Invoke(tenantId, provider);
            return provider;
        })!;
    }

    public void Invalidate(string tenantId) => _cache.Remove(_options.CacheKeyFactory(tenantId));
    public void InvalidateAll() { /* Implement global invalidation if needed */ }
}
4️⃣ Example Configurator

/// <summary>
/// Configures SMTP settings for tenants that have SMTP enabled in their metadata.
/// </summary>
public class SmtpTenantConfigurator : ITenantServiceConfigurator
{
    public void Configure(IServiceCollection services, TenantInfo tenant)
    {
        if (tenant.Metadata.TryGetValue("smtp_enabled", out var enabled) && enabled == "true")
        {
            services.Configure<SmtpOptions>(opt =>
            {
                opt.Host = tenant.Metadata.GetValueOrDefault("smtp_host") ?? $"smtp.{tenant.TenantId}.myapp.com";
                opt.Port = int.TryParse(tenant.Metadata.GetValueOrDefault("smtp_port"), out var port) ? port : 587;
                opt.UseSsl = true;
                opt.SenderName = tenant.DisplayName;
            });
        }
    }
}


5️⃣ Future Extensions
Extension	Purpose
InvalidateAll Implementation	Clear all tenant providers from cache
Versioned Cache Keys	Change prefix to invalidate all without looping
Per-Tenant Hosted Services	Run background jobs per tenant
Secrets Integration	Fetch sensitive data from secure store per tenant
Feature Flags	Enable/disable features per tenant
Health Checks	Check service health per tenant
Observability	Log and collect metrics per tenant

6️⃣ Example Registration

services.AddTenantContainers(configure: opts =>
{
    opts.SlidingExpiration = TimeSpan.FromMinutes(20);
    opts.DisposeOnInvalidate = true;
});

services.AddTenantConfigurator<SmtpTenantConfigurator>();
services.AddAsyncTenantConfigurator<FeatureFlagsTenantConfigurator>();

7️⃣ Usage Example

public class EmailSender
{
    private readonly ITenantIdProvider _tenantIdProvider;
    private readonly ITenantScopedServiceProviderFactory _factory;

    public EmailSender(ITenantIdProvider tenantIdProvider, ITenantScopedServiceProviderFactory factory)
    {
        _tenantIdProvider = tenantIdProvider;
        _factory = factory;
    }

    public void SendEmail(string to, string subject, string body)
    {
        var tenantId = _tenantIdProvider.GetCurrentTenantId();
        var sp = _factory.GetProviderForTenant(tenantId);
        var smtp = sp.GetRequiredService<IOptions<SmtpOptions>>().Value;
        // Use smtp.Host, smtp.Port, smtp.SenderName...
    }
}