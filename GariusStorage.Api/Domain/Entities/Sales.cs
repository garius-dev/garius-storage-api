using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using GariusStorage.Api.Domain.Interfaces;

namespace GariusStorage.Api.Domain.Entities
{
    public class Sales : BaseEntity, ITenantEntity
    {
        [Required]
        public Guid SellerId { get; set; }

        public Guid? CustomerId { get; set; } // Opcional, para vendas sem cliente identificado

        [ForeignKey("SellerId")]
        public Sellers Seller { get; set; }

        [ForeignKey("CustomerId")]
        public Customers? Customer { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        public DateTime SaleDate { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }


        public ICollection<SaleItems> Items { get; set; } = new List<SaleItems>();
        public ICollection<StockMovements> StockMovements { get; set; } = new List<StockMovements>();
        public ICollection<CashFlows> CashFlows { get; set; } = new List<CashFlows>();
        public ICollection<Invoices> Invoices { get; set; } = new List<Invoices>();

        // Propriedades para Multi-Tenancy
        [Required]
        public Guid CompanyId { get; set; }

        [ForeignKey("CompanyId")]
        public Companies Company { get; set; }
    }
}
