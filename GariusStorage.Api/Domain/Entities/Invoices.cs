using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using GariusStorage.Api.Domain.Interfaces;

namespace GariusStorage.Api.Domain.Entities
{
    public class Invoices : BaseEntity, ITenantEntity
    {
        [Required]
        public Guid SaleId { get; set; }

        [ForeignKey("SaleId")]
        public Sales Sale { get; set; }

        [Required, MaxLength(50)]
        public string InvoiceNumber { get; set; }

        [Required, MaxLength(3)]
        public string Series { get; set; } // Ex.: "001"

        [MaxLength(44)]
        public string? NFeKey { get; set; } // Chave da NF-e (44 dígitos)

        [MaxLength(20)]
        public string? Protocol { get; set; } // Protocolo de autorização

        [Required]
        public DateTime IssueDate { get; set; }

        public DateTime? AuthorizationDate { get; set; } // Data de autorização pela SEFAZ

        [Required]
        public string Status { get; set; } // Ex.: "Pendente", "Autorizada", "Cancelada", "Rejeitada"

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TaxAmount { get; set; } // Total de impostos

        [MaxLength(500)]
        public string? Notes { get; set; }

        [MaxLength(4000)]
        public string? RejectionReason { get; set; } // Motivo de rejeição, se houver

        // Propriedades para Multi-Tenancy
        [Required]
        public Guid CompanyId { get; set; }

        [ForeignKey("CompanyId")]
        public Companies Company { get; set; }
    }
}
