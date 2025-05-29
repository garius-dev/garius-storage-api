using System.ComponentModel.DataAnnotations;

namespace GariusStorage.Api.Application.Dtos.Company
{
    public class UpdateCompanyRequestDto
    {
        [Required(ErrorMessage = "O nome legal é obrigatório.")]
        [MaxLength(255)]
        public string LegalName { get; set; }

        [MaxLength(255)]
        public string? TradeName { get; set; }

        // CNPJ não deve ser alterado após a criação, geralmente.
        // Se for permitido, adicione aqui. Caso contrário, remova.
        // Para este exemplo, vamos assumir que não pode ser alterado.

        [MaxLength(50)]
        public string? StateRegistration { get; set; }

        [MaxLength(50)]
        public string? MunicipalRegistration { get; set; }

        [MaxLength(255)]
        public string? Address { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "Formato de email inválido.")]
        [MaxLength(100)]
        public string? Email { get; set; }

        public Guid? DefaultCurrencyId { get; set; }

        public IFormFile? ImageFile { get; set; }

        public bool? Enabled { get; set; } // Para permitir inativar/ativar
    }
}
