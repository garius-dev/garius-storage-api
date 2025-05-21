using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using GariusStorage.Api.Helpers;
using GariusStorage.Api.Domain.Interfaces;

namespace GariusStorage.Api.Domain.Entities
{
    public class Seller : BaseEntity, ITenantEntity
    {
        [Required, MaxLength(255)]
        public string Name { get; set; }

        [MaxLength(18), CPF]
        public string? CPF { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [MaxLength(100)]
        public string? Email { get; set; }


        public ICollection<Sale> Sales { get; set; } = new List<Sale>();

        // Propriedades para Multi-Tenancy
        [Required]
        public Guid CompanyId { get; set; }

        [ForeignKey("CompanyId")]
        public Company Company { get; set; }
    }
}
