using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sample.Web.Entities.OTO
{
    public class UserProfile : EntityBase
    {
        [MaxLength(450)]
        [Key] // PK
        [ForeignKey(nameof(User))] // وفي نفس الوقت FK
        public Guid Id { get; set; }
        public User User { get; set; }
        public string Bio { get; set; }
    }



}
