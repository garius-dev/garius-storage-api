using GariusStorage.Api.Application.Dtos.Auth;

namespace GariusStorage.Api.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResult> RegisterAsync(RegisterRequestDto dto);
        Task<AuthResult> LoginAsync(LoginRequestDto dto);
        Task<AuthResult> HandleExternalLoginCallbackAsync();
    }
}
