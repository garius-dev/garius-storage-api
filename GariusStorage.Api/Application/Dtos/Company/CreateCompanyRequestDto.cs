using System.ComponentModel.DataAnnotations;

namespace GariusStorage.Api.Application.Dtos.Company
{
    public class CreateCompanyRequestDto
    {
        [Required(ErrorMessage = "O nome legal é obrigatório.")]
        [MaxLength(255)]
        public string LegalName { get; set; }

        [MaxLength(255)]
        public string? TradeName { get; set; }

        [Required(ErrorMessage = "O CNPJ é obrigatório.")]
        [MaxLength(18)]
        // A validação de formato do CNPJ já existe na entidade via [CNPJ] attribute.
        // Se precisar de validação antecipada aqui, pode adicionar um custom attribute ou regex.
        public string CNPJ { get; set; }

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

        //public Guid? DefaultCurrencyId { get; set; }

        public IFormFile? ImageFile { get; set; }
    }
}
