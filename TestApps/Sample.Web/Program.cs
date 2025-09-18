using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Sample.Web.Data;
using Syn.Core.MultiTenancy;
using Syn.Core.MultiTenancy.Features;
using Syn.Core.MultiTenancy.Features.Database;
using Syn.Core.MultiTenancy.Metadata;
using Syn.Core.Logger;

var builder = WebApplication.CreateBuilder(args);
var trackedServices = builder.Services.TrackAppDbContextRegistrations();

var config = builder.Configuration;

// قراءة الإعدادات
var providerTypeString = config["FeatureFlags:ProviderType"] ?? "EfCore";
var cacheDurationMinutes = int.TryParse(config["FeatureFlags:CacheDurationMinutes"], out var m) ? m : 5;
var runBulkMigrations = config.GetValue<bool>("Migrations:RunForAllTenants");

// طباعة الإعدادات في الكونسول
ConsoleLog.Info("=== Application Starting ===", customPrefix: "Startup");
ConsoleLog.Info($"FeatureFlags Provider: {providerTypeString}", customPrefix: "Startup");
ConsoleLog.Info($"Cache Duration: {cacheDurationMinutes} minutes", customPrefix: "Startup");
ConsoleLog.Info($"Run Bulk Migrations: {runBulkMigrations}", customPrefix: "Startup");

// تعريف التينانتس
var tenants = new[]
{
    new TenantInfo("tenant1", config.GetConnectionString("Tenant1Db")!),
    new TenantInfo("tenant2", config.GetConnectionString("Tenant2Db")!)
};

// تعريف TenantStoreFactory
Func<IServiceProvider, ITenantStore> tenantStoreFactory = sp =>
{
    var memoryCache = sp.GetRequiredService<IMemoryCache>();
    var innerStore = new InMemoryTenantStore(tenants);
    return new CachedTenantStore(innerStore, memoryCache);
};

// تحديد نوع الـ Provider
var providerType = providerTypeString.Equals("Sql", StringComparison.OrdinalIgnoreCase)
    ? TenantFeatureFlagProviderType.Sql
    : TenantFeatureFlagProviderType.EfCore;

// تسجيل MultiTenancy + FeatureFlags
trackedServices.AddMultiTenancyWithFeatureFlags(
    configure: options =>
    {
        options.DefaultTenantPropertyName = "TenantId";
        options.UseTenantInterceptor = true;
        options.TenantStoreFactory = tenantStoreFactory;
    },
    providerType: providerType,
    cacheDuration: TimeSpan.FromMinutes(cacheDurationMinutes),
    knownTenants: tenants,
    optionsAction: providerType == TenantFeatureFlagProviderType.EfCore
        ? options => options.UseSqlServer(config.GetConnectionString("Default"))
        : null,
    dbContextType: providerType == TenantFeatureFlagProviderType.EfCore
        ? typeof(AppDbContext)
        : null,
    defaultConnectionString: providerType == TenantFeatureFlagProviderType.Sql
        ? config.GetConnectionString("Default")
        : null
);

// HostedService للاختبارات
trackedServices.AddHostedService<MultiTenantTestRunner>();

// Controllers + Swagger
trackedServices.AddControllers();
trackedServices.AddEndpointsApiExplorer();
trackedServices.AddSwaggerGen(options =>
{
    var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }

    options.TagActionsBy(api =>
    {
        return new[] { api.GroupName ?? api.ActionDescriptor.RouteValues["controller"] ?? "Default" };
    });

    options.DocInclusionPredicate((name, api) => true);

    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Tenant Feature Flags Playground API",
        Version = "v1",
        Description = "A playground API to test the full multi-tenancy + feature flags library (EF & SQL providers)."
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Tenant Feature Flags Playground API v1");
        options.DocumentTitle = "Tenant Feature Flags Playground";
        options.RoutePrefix = string.Empty;
    });
}

// Middleware لحل التينانت
app.UseTenantResolution();

app.MapControllers();
app.Run();