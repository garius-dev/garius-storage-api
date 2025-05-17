using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace GariusStorage.Api.Domain.Entities
{
    public class StorageLocations : BaseEntity
    {
        [Required, MaxLength(255)]
        public string Name { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(255)]
        public string? Address { get; set; }


        public ICollection<Stocks> Stocks { get; set; } = new List<Stocks>();
    }
}
