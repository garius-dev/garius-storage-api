using GariusStorage.Api.Domain.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GariusStorage.Api.Domain.Entities
{
    public class PurchaseItems : BaseEntity, ITenantEntity
    {
        [Required]
        public Guid PurchaseId { get; set; }

        [Required]
        public Guid ProductId { get; set; }

        [ForeignKey("PurchaseId")]
        public Purchases Purchase { get; set; }

        [ForeignKey("ProductId")]
        public Products Product { get; set; }

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCost { get; set; }

        // Propriedades para Multi-Tenancy
        [Required]
        public Guid CompanyId { get; set; }

        [ForeignKey("CompanyId")]
        public Companies Company { get; set; }
    }
}
