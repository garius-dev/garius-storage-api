﻿using GariusStorage.Api.Helpers;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using GariusStorage.Api.Domain.Interfaces;

namespace GariusStorage.Api.Domain.Entities
{
    public class Customer : BaseEntity, ITenantEntity
    {
        [Required, MaxLength(255)]
        public string Name { get; set; }

        [MaxLength(18), CNPJ, CPF] // Pode ser CNPJ ou CPF
        public string? Document { get; set; }

        [MaxLength(255)]
        public string? Address { get; set; }

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
