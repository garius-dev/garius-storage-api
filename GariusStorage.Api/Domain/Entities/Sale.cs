using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using GariusStorage.Api.Domain.Interfaces;

namespace GariusStorage.Api.Domain.Entities
{
    public class Sale : BaseEntity, ITenantEntity
    {
        [Required]
        public Guid SellerId { get; set; }

        public Guid? CustomerId { get; set; } // Opcional, para vendas sem cliente identificado

        [ForeignKey("SellerId")]
        public Seller Seller { get; set; }

        [ForeignKey("CustomerId")]
        public Customer? Customer { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        public DateTime SaleDate { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }


        public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
        public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
        public ICollection<CashFlow> CashFlows { get; set; } = new List<CashFlow>();
        public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

        // Propriedades para Multi-Tenancy
        [Required]
        public Guid CompanyId { get; set; }

        [ForeignKey("CompanyId")]
        public Company Company { get; set; }
    }
}
