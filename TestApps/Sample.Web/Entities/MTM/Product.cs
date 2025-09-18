using System.ComponentModel.DataAnnotations;

namespace Sample.Web.Entities.MTM
{
    public class Product : EntityBase
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        [MaxLength(250)]
        public string Name { get; set; }
        public ICollection<Tag> Tags { get; set; }
    }


    //[Description("Stores product information")]
    //public class Product : IDbEntity
    //{
    //    [Description("Primary key")]
    //    [Required]
    //    [Key]
    //    public int Id { get; set; }

    //    [MaxLength(100)]
    //    [Required]
    //    [Description("Product name")]
    //    public string Name { get; set; }

    //    [DefaultValue(0)]
    //    [Description("Stock quantity available")]
    //    public int Stock { get; set; }

    //    [Unique(ConstraintName = "UQ_Product_Code")]
    //    [MaxLength(50)]
    //    [Description("Unique product code")]
    //    public string Code { get; set; }
    //}

}
