using System.ComponentModel.DataAnnotations;

namespace GariusStorage.Api.Application.Dtos.Auth
{
    public class LoginRequestDto
    {
        [Required(ErrorMessage = "O nome de usuário (email) é obrigatório.")]
        public string Username { get; set; }

        [Required(ErrorMessage = "A senha é obrigatória.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required(ErrorMessage = "O token do Turnstile é obrigatório.")]
        public string TurnstileToken { get; set; } // Novo campo
    }
}
