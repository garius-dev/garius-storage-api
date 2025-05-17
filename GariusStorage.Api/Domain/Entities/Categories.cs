using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace GariusStorage.Api.Domain.Entities
{
    public class Categories : BaseEntity
    {
        [Required, MaxLength(255)]
        public string Name { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public Guid? ParentCategoryId { get; set; }
        [ForeignKey("ParentCategoryId")]
        public Categories? ParentCategory { get; set; }


        public ICollection<Products> Products { get; set; } = new List<Products>();
    }
}
