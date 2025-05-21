using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using GariusStorage.Api.Domain.Interfaces;

namespace GariusStorage.Api.Domain.Entities
{
    public class StorageLocation : BaseEntity, ITenantEntity
    {
        [Required, MaxLength(255)]
        public string Name { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(255)]
        public string? Address { get; set; }


        public ICollection<Stock> Stocks { get; set; } = new List<Stock>();

        // Propriedades para Multi-Tenancy
        [Required]
        public Guid CompanyId { get; set; }

        [ForeignKey("CompanyId")]
        public Company Company { get; set; }
    }
}
