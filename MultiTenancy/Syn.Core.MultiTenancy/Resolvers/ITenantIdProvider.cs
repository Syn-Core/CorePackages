using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Syn.Core.MultiTenancy.Resolvers
{
    public interface ITenantIdProvider
    {
        string GetCurrentTenantId();
    }
}
