using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sample.Web.Entities.MTMT
{
    public class Student : EntityBase
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FullName { get; set; }
        public ICollection<Enrollment> Enrollments { get; set; }
    }
}
