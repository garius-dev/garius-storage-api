using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using GariusStorage.Api.Domain.Interfaces;

namespace GariusStorage.Api.Domain.Entities
{
    public class Category : BaseEntity, ITenantEntity
    {
        [Required, MaxLength(255)]
        public string Name { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public Guid? ParentCategoryId { get; set; }
        [ForeignKey("ParentCategoryId")]
        public Category? ParentCategory { get; set; }

        // Coleção para subcategorias, para o relacionamento bidirecional
        public ICollection<Category> SubCategories { get; set; } = [];
        public ICollection<Product> Products { get; set; } = [];

        // Propriedades para Multi-Tenancy
        [Required]
        public Guid CompanyId { get; set; }

        [ForeignKey("CompanyId")]
        public Company Company { get; set; }
    }
}
