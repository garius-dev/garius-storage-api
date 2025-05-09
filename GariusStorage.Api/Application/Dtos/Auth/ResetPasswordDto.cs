using System.ComponentModel.DataAnnotations;

namespace GariusStorage.Api.Application.Dtos.Auth
{
    public class ResetPasswordDto
    {
        [Required(ErrorMessage = "O email é obrigatório.")]
        [EmailAddress(ErrorMessage = "Formato de email inválido.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "O token de redefinição é obrigatório.")]
        public string Token { get; set; }

        [Required(ErrorMessage = "A nova senha é obrigatória.")]
        [DataType(DataType.Password)]
        [StringLength(100, ErrorMessage = "A {0} deve ter pelo menos {2} e no máximo {1} caracteres.", MinimumLength = 8)]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "A nova senha e a senha de confirmação não correspondem.")]
        public string ConfirmNewPassword { get; set; }
    }
}
