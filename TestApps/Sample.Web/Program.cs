using Microsoft.EntityFrameworkCore;

using Sample.Web.Data;

using Syn.Core.MultiTenancy.Features;
using Syn.Core.MultiTenancy.Features.Database;
using Syn.Core.MultiTenancy.Metadata;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration;
var providerTypeString = config["FeatureFlags:ProviderType"] ?? "EfCore";
var cacheDurationMinutes = int.TryParse(config["FeatureFlags:CacheDurationMinutes"], out var m) ? m : 5;

var tenants = new[]
{
    new TenantInfo("tenant1", config.GetConnectionString("Tenant1Db")!),
    new TenantInfo("tenant2", config.GetConnectionString("Tenant2Db")!)
};

var providerType = providerTypeString.Equals("Sql", StringComparison.OrdinalIgnoreCase)
    ? TenantFeatureFlagProviderType.Sql
    : TenantFeatureFlagProviderType.EfCore;

if (providerType == TenantFeatureFlagProviderType.EfCore)
{
    builder.Services.AddTenantFeatureFlags(
        cacheDuration: TimeSpan.FromMinutes(cacheDurationMinutes),
        knownTenants: tenants,
        optionsAction: options => options.UseSqlServer(config.GetConnectionString("Default")),
        dbContextType: typeof(MyDbContext)
    );
}
else
{
    builder.Services.AddTenantFeatureFlags(
        cacheDuration: TimeSpan.FromMinutes(cacheDurationMinutes),
        config.GetConnectionString("Default"),
        knownTenants: tenants
    );
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
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

app.MapControllers();
app.Run();