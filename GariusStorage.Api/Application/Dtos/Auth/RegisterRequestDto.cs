using System.ComponentModel.DataAnnotations;

namespace GariusStorage.Api.Application.Dtos.Auth
{
    public class RegisterRequestDto
    {
        [Required(ErrorMessage = "O nome de usuário é obrigatório.")]
        [EmailAddress(ErrorMessage = "O nome de usuário deve ser um email válido.")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "O email é obrigatório.")]
        [EmailAddress(ErrorMessage = "Formato de email inválido.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "A senha é obrigatória.")]
        [DataType(DataType.Password)]
        [StringLength(100, ErrorMessage = "A {0} deve ter pelo menos {2} e no máximo {1} caracteres.", MinimumLength = 8)]
        // Adicione aqui os mesmos requisitos de senha do Identity (Program.cs) se quiser feedback no DTO
        // Ex: [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{8,}$", ErrorMessage = "A senha não atende aos requisitos.")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "A senha e a senha de confirmação não correspondem.")]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "O token do Turnstile é obrigatório.")]
        public string TurnstileToken { get; set; } // Novo campo
    }
}
