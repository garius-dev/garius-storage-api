using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using GariusStorage.Api.Domain.Interfaces;

namespace GariusStorage.Api.Domain.Entities
{
    public enum MovementType
    {
        Entry = 1,
        Exit = 2
    }

    public class StockMovement : BaseEntity, ITenantEntity
    {
        [Required]
        public Guid ProductId { get; set; }

        public Guid? SaleId { get; set; }

        public Guid? PurchaseId { get; set; }

        [ForeignKey("ProductId")]
        public Product Product { get; set; }

        [ForeignKey("SaleId")]
        public Sale? Sale { get; set; }

        [ForeignKey("PurchaseId")]
        public Purchase? Purchase { get; set; }

        [Required]
        public MovementType MovementType { get; set; }

        [Required]
        public int Quantity { get; set; }

        [MaxLength(500)]
        public string? Observation { get; set; }

        // Propriedades para Multi-Tenancy
        [Required]
        public Guid CompanyId { get; set; }

        [ForeignKey("CompanyId")]
        public Company Company { get; set; }

    }
}
