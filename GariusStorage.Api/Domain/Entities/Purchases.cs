using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace GariusStorage.Api.Domain.Entities
{
    public class Purchases : BaseEntity
    {
        [Required]
        public Guid SupplierId { get; set; }

        [ForeignKey("SupplierId")]
        public Suppliers Supplier { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        public DateTime PurchaseDate { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }


        public ICollection<PurchaseItems> Items { get; set; } = new List<PurchaseItems>();
        public ICollection<StockMovements> StockMovements { get; set; } = new List<StockMovements>();
        public ICollection<CashFlows> CashFlows { get; set; } = new List<CashFlows>();
    }
}
