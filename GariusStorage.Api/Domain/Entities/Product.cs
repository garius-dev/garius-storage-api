using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using GariusStorage.Api.Domain.Interfaces;

namespace GariusStorage.Api.Domain.Entities
{
    public class Product : BaseEntity, ITenantEntity
    {
        [Required, MaxLength(255)]
        public string Name { get; set; }

        [Required, MaxLength(100)]
        public string SKU { get; set; }

        [MaxLength(100)]
        public string? Barcode { get; set; }

        [Required, MaxLength(8)]
        public string NCM { get; set; }

        [MaxLength(7)]
        public string? CEST { get; set; }

        [Required, MaxLength(10)]
        public string CFOP { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Cost { get; set; }

        public int MinStock { get; set; }

        public int MaxStock { get; set; }

        public bool IsActive { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(255)]
        public string? Brand { get; set; }

        public Guid? CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }

        [MaxLength(50)]
        public string? UnitOfMeasure { get; set; }

        [MaxLength(50)]
        public string? TaxUnitOfMeasure { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal TaxQuantityPerUnit { get; set; }

        public int Origin { get; set; }

        [MaxLength(3)]
        public string? ICMS_CST { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? ICMS_Rate { get; set; }

        [MaxLength(3)]
        public string? PIS_CST { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? PIS_Rate { get; set; }

        [MaxLength(3)]
        public string? COFINS_CST { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? COFINS_Rate { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? IPI_Rate { get; set; }

        [MaxLength(20)]
        public string? ANVISA_Code { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Weight { get; set; }

        [MaxLength(13)]
        public string? EAN { get; set; } // Código de barras EAN para NF-e

        [Column(TypeName = "decimal(18,2)")]
        public decimal? NetWeight { get; set; } // Peso líquido

        [Column(TypeName = "decimal(18,2)")]
        public decimal? GrossWeight { get; set; } // Peso bruto

        [MaxLength(20)]
        public string? TaxSituation { get; set; } // Ex.: "Tributado", "Isento"

        // Propriedades para Multi-Tenancy
        [Required]
        public Guid CompanyId { get; set; }

        [ForeignKey("CompanyId")]
        public Company Company { get; set; }

    }
}
