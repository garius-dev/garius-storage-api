using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace GariusStorage.Api.Domain.Entities
{
    public class Currency : BaseEntity
    {
        [Required, MaxLength(3)]
        public string Code { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; }

        [Required, MaxLength(10)]
        public string Symbol { get; set; }

        public bool IsDefault { get; set; }
    }
}
