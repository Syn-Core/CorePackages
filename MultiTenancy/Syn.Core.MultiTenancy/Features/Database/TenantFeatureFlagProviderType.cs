using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Syn.Core.MultiTenancy.Features.Database
{
    /// <summary>
    /// Enum to select the tenant feature flag storage provider.
    /// </summary>
    public enum TenantFeatureFlagProviderType
    {
        EfCore,
        Sql
    }

}
