using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using GariusStorage.Api.Helpers;

namespace GariusStorage.Api.Domain.Entities
{
    public class Sellers : BaseEntity
    {
        [Required, MaxLength(255)]
        public string Name { get; set; }

        [MaxLength(18), CPF]
        public string? CPF { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [MaxLength(100)]
        public string? Email { get; set; }


        public ICollection<Sales> Sales { get; set; } = new List<Sales>();
    }
}
