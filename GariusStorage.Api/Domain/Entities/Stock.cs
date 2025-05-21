using GariusStorage.Api.Domain.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GariusStorage.Api.Domain.Entities
{
    public class Stock : BaseEntity, ITenantEntity
    {
        [Required]
        public Guid ProductId { get; set; }

        [Required]
        public Guid StorageLocationId { get; set; }

        [MaxLength(50)]
        public string? LotNumber { get; set; } // Número do lote

        public DateTime? ExpiryDate { get; set; } // Data de validade

        [ForeignKey("ProductId")]
        public Product Product { get; set; }

        [ForeignKey("StorageLocationId")]
        public StorageLocation StorageLocation { get; set; }

        public int Quantity { get; set; }

        // Propriedades para Multi-Tenancy
        [Required]
        public Guid CompanyId { get; set; }

        [ForeignKey("CompanyId")]
        public Company Company { get; set; }

    }
}
