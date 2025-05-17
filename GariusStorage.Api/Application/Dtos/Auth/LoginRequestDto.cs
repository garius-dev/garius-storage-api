using System.ComponentModel.DataAnnotations;

namespace GariusStorage.Api.Application.Dtos.Auth
{
    public class LoginRequestDto
    {
        [Required(ErrorMessage = "O nome de usuário (email) é obrigatório.")]
        public string Username { get; set; }

        [Required(ErrorMessage = "A senha é obrigatória.")]
        [DataType(DataType.Password)]
        [RegularExpression(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{6,}$",
        ErrorMessage = "A senha deve ter no mínimo 6 caracteres, com pelo menos uma letra maiúscula, uma letra minúscula, um número e um caractere especial.")]
        public string Password { get; set; }

        [Required(ErrorMessage = "O token do Turnstile é obrigatório.")]
        public string TurnstileToken { get; set; } // Novo campo
    }
}
