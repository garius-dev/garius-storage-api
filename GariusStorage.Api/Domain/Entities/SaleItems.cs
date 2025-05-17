using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GariusStorage.Api.Domain.Entities
{
    public class SaleItems : BaseEntity
    {
        [Required]
        public Guid SaleId { get; set; }

        [Required]
        public Guid ProductId { get; set; }

        [ForeignKey("SaleId")]
        public Sales Sale { get; set; }

        [ForeignKey("ProductId")]
        public Products Product { get; set; }

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }
    }
}
