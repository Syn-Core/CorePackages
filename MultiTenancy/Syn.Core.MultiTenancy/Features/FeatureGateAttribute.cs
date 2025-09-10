using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Syn.Core.MultiTenancy.Features
{
    /// <summary>
    /// Attribute to guard actions/controllers based on a tenant feature flag.
    /// </summary>
    public sealed class FeatureGateAttribute : ActionFilterAttribute
    {
        private readonly string _featureName;

        public FeatureGateAttribute(string featureName)
        {
            _featureName = featureName;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var flags = context.HttpContext.RequestServices.GetService<ITenantFeatureFlags>();
            if (flags == null || !flags.IsEnabled(_featureName))
            {
                context.Result = new ForbidResult();
            }
        }
    }
}
