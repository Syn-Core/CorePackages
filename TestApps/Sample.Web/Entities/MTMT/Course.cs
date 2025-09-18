using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sample.Web.Entities.MTMT
{
    public class Course : EntityBase
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        [MaxLength(100)]
        public string Title { get; set; }
        public ICollection<Enrollment> Enrollments { get; set; }
    }

}
