using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using GariusStorage.Api.Domain.Interfaces;

namespace GariusStorage.Api.Domain.Entities
{
    public class Purchase : BaseEntity, ITenantEntity
    {
        [Required]
        public Guid SupplierId { get; set; }

        [ForeignKey("SupplierId")]
        public Supplier Supplier { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        public DateTime PurchaseDate { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }


        public ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
        public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
        public ICollection<CashFlow> CashFlows { get; set; } = new List<CashFlow>();

        // Propriedades para Multi-Tenancy
        [Required]
        public Guid CompanyId { get; set; }

        [ForeignKey("CompanyId")]
        public Company Company { get; set; }
    }
}
