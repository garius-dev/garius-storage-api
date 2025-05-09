using System.ComponentModel.DataAnnotations;

namespace GariusStorage.Api.Domain.Entities
{
    public abstract class BaseEntity
    {
        [Key]
        public Guid Id { get; set; }
        public bool Enabled { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdate { get; set; }
    }
}
