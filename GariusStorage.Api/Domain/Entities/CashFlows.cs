using GariusStorage.Api.Domain.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GariusStorage.Api.Domain.Entities
{
    public enum CashFlowType
    {
        Entry = 1,
        Exit = 2
    }

    public class CashFlows : BaseEntity, ITenantEntity
    {
        public Guid? SaleId { get; set; }

        public Guid? PurchaseId { get; set; }

        [ForeignKey("SaleId")]
        public Sales? Sale { get; set; }

        [ForeignKey("PurchaseId")]
        public Purchases? Purchase { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        public CashFlowType Type { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        // Propriedades para Multi-Tenancy
        [Required]
        public Guid CompanyId { get; set; }

        [ForeignKey("CompanyId")]
        public Companies Company { get; set; }
    }
}
