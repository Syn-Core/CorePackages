using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sample.Web.Entities.MTM
{
    public class Tag : EntityBase
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Label { get; set; }
        public ICollection<Product> Products { get; set; }
    }

}
