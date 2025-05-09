using System.ComponentModel.DataAnnotations;

namespace GariusStorage.Api.Application.Dtos.Auth
{
    public class ForgotPasswordDto
    {
        [Required(ErrorMessage = "O email é obrigatório.")]
        [EmailAddress(ErrorMessage = "Formato de email inválido.")]
        public string Email { get; set; }
    }
}
