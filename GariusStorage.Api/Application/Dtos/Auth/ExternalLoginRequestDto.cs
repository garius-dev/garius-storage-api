using System.ComponentModel.DataAnnotations;

namespace GariusStorage.Api.Application.Dtos.Auth
{
    public class ExternalLoginRequestDto
    {
        [Required(ErrorMessage = "O provedor de login é obrigatório.")]
        public string Provider { get; set; } // Ex: "Google", "Microsoft"

        // Poderia incluir um returnUrl se necessário para redirecionamentos no frontend
        // public string? ReturnUrl { get; set; }
    }
}
