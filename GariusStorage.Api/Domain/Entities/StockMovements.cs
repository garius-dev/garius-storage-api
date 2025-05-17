using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace GariusStorage.Api.Domain.Entities
{
    public enum MovementType
    {
        Entry = 1,
        Exit = 2
    }

    public class StockMovements : BaseEntity
    {
        [Required]
        public Guid ProductId { get; set; }

        public Guid? SaleId { get; set; }

        public Guid? PurchaseId { get; set; }

        [ForeignKey("ProductId")]
        public Products Product { get; set; }

        [ForeignKey("SaleId")]
        public Sales? Sale { get; set; }

        [ForeignKey("PurchaseId")]
        public Purchases? Purchase { get; set; }

        [Required]
        public MovementType MovementType { get; set; }

        [Required]
        public int Quantity { get; set; }

        [MaxLength(500)]
        public string? Observation { get; set; }

    }
}
