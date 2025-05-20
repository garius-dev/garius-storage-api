using GariusStorage.Api.Domain.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GariusStorage.Api.Domain.Entities
{
    public class Stocks : BaseEntity, ITenantEntity
    {
        [Required]
        public Guid ProductId { get; set; }

        [Required]
        public Guid StorageLocationId { get; set; }

        [MaxLength(50)]
        public string? LotNumber { get; set; } // Número do lote

        public DateTime? ExpiryDate { get; set; } // Data de validade

        [ForeignKey("ProductId")]
        public Products Product { get; set; }

        [ForeignKey("StorageLocationId")]
        public StorageLocations StorageLocation { get; set; }

        public int Quantity { get; set; }

        // Propriedades para Multi-Tenancy
        [Required]
        public Guid CompanyId { get; set; }

        [ForeignKey("CompanyId")]
        public Companies Company { get; set; }

    }
}
