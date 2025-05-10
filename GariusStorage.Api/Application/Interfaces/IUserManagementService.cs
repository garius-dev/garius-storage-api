using GariusStorage.Api.Application.Dtos;
using System.Security.Claims;

namespace GariusStorage.Api.Application.Interfaces
{
    public interface IUserManagementService
    {
        Task<UserProfileDto?> GetUserProfileAsync(string emailOrUsername);
        Task<UpdateUserProfileResultDto> UpdateUserProfileAsync(string targetEmailOrUsername, UserProfileDto profileUpdateDto, ClaimsPrincipal performingUser);
        // Poderíamos adicionar outros métodos aqui no futuro, como listar todos os usuários, etc.
    }
}
