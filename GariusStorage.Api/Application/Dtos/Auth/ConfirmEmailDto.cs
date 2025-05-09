using System.ComponentModel.DataAnnotations;

namespace GariusStorage.Api.Application.Dtos.Auth
{
    public class ConfirmEmailDto
    {
        [Required(ErrorMessage = "O ID do usuário é obrigatório.")]
        public string UserId { get; set; }

        [Required(ErrorMessage = "O token de confirmação é obrigatório.")]
        public string Token { get; set; }
    }
}
