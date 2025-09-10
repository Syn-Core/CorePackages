using Microsoft.Extensions.DependencyInjection;

using Syn.Core.MultiTenancy.Metadata;

namespace Syn.Core.MultiTenancy.DI
{
    /// <summary>
    /// Configures strongly-typed SMTP options per tenant using TenantInfo metadata.
    /// Expected metadata keys: smtp_enabled, smtp_host, smtp_port, smtp_ssl, smtp_sender.
    /// </summary>
    public sealed class SmtpTenantConfigurator : ITenantServiceConfigurator
    {
        /// <inheritdoc />
        public void Configure(IServiceCollection services, TenantInfo tenant)
        {
            if (tenant.Metadata.TryGetValue("smtp_enabled", out var enabled) && enabled.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                services.Configure<SmtpOptions>(opt =>
                {
                    opt.Host = tenant.Metadata.TryGetValue("smtp_host", out var host)
                        ? host
                        : $"smtp.{tenant.TenantId}.myapp.com";

                    opt.Port = tenant.Metadata.TryGetValue("smtp_port", out var portStr) && int.TryParse(portStr, out var port)
                        ? port
                        : 587;

                    opt.UseSsl = !tenant.Metadata.TryGetValue("smtp_ssl", out var ssl) || !ssl.Equals("false", StringComparison.OrdinalIgnoreCase);
                    opt.SenderName = tenant.Metadata.TryGetValue("smtp_sender", out var sender) ? sender : 
                    tenant.DisplayName ?? throw new ArgumentNullException(@"tenant.Metadata[""smtp_sender""] or tenant.DisplayName must set.");
                });
            }
        }
    }
}
