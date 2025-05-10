using System.ComponentModel.DataAnnotations;

namespace GariusStorage.Api.Application.Dtos
{
    // DTO para representar uma claim individualmente
    public class ClaimDto
    {
        [Required(ErrorMessage = "O tipo da claim é obrigatório.")]
        public string Type { get; set; }

        [Required(ErrorMessage = "O valor da claim é obrigatório.")]
        public string Value { get; set; }
    }

    // DTO para visualizar e atualizar o perfil de um usuário (roles e claims)
    public class UserProfileDto
    {
        public string UserId { get; set; } // Será preenchido pelo backend
        public string UserName { get; set; } // Será preenchido pelo backend
        public string Email { get; set; } // Será preenchido pelo backend

        [Required(ErrorMessage = "A lista de roles é obrigatória (pode ser vazia).")]
        public List<string> Roles { get; set; } = new List<string>();

        [Required(ErrorMessage = "A lista de claims é obrigatória (pode ser vazia).")]
        public List<ClaimDto> Claims { get; set; } = new List<ClaimDto>();
    }

    // DTO para o resultado da atualização do perfil
    public class UpdateUserProfileResultDto
    {
        public bool Succeeded { get; set; }
        public string Message { get; set; }
        public UserProfileDto? UserProfile { get; set; } // Perfil atualizado
        public IEnumerable<string>? Errors { get; set; }

        public static UpdateUserProfileResultDto Success(UserProfileDto updatedProfile, string message = "Perfil do usuário atualizado com sucesso.")
        {
            return new UpdateUserProfileResultDto { Succeeded = true, UserProfile = updatedProfile, Message = message };
        }

        public static UpdateUserProfileResultDto Failed(string message, IEnumerable<string>? errors = null)
        {
            return new UpdateUserProfileResultDto { Succeeded = false, Message = message, Errors = errors ?? Enumerable.Empty<string>() };
        }
    }
}
