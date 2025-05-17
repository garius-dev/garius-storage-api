using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using GariusStorage.Api.Helpers;

namespace GariusStorage.Api.Domain.Entities
{
    public class Suppliers : BaseEntity
    {

        [Required, MaxLength(255)]
        public string Name { get; set; }

        [MaxLength(18), CNPJ]
        public string? CNPJ { get; set; }

        [MaxLength(255)]
        public string? Address { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [MaxLength(100)]
        public string? Email { get; set; }


        public ICollection<Purchases> Purchases { get; set; } = new List<Purchases>();
    }
}
