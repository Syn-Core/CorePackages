using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sample.Web.Entities.OTO
{
    public class UserProfile : EntityBase
    {
        [MaxLength(450)]
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Bio { get; set; }
        [MaxLength(450)]
        public Guid UserId { get; set; } // ⬅️ ضروري
        public User User { get; set; }
    }



}
