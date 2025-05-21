using GariusStorage.Api.Domain.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GariusStorage.Api.Domain.Entities
{
    public class SaleItem : BaseEntity, ITenantEntity
    {
        [Required]
        public Guid SaleId { get; set; }

        [Required]
        public Guid ProductId { get; set; }

        [ForeignKey("SaleId")]
        public Sale Sale { get; set; }

        [ForeignKey("ProductId")]
        public Product Product { get; set; }

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        // Propriedades para Multi-Tenancy
        [Required]
        public Guid CompanyId { get; set; }

        [ForeignKey("CompanyId")]
        public Company Company { get; set; }
    }
}
