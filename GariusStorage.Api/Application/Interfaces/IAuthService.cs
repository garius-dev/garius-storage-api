using GariusStorage.Api.Application.Dtos.Auth;
using GariusStorage.Api.Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GariusStorage.Api.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResult> RegisterLocalUserAsync(RegisterRequestDto dto);
        Task<AuthResult> FallBackRegister(string userId);
        Task<AuthResult> LoginLocalAsync(LoginRequestDto dto);

        // Para iniciar o fluxo de login externo
        Task<ChallengeResult?> ChallengeExternalLoginAsync(string provider, string redirectUrl);

        // Para manipular o callback do login externo
        Task<AuthResult> HandleExternalLoginCallbackAsync();

        Task<(IdentityResult result, ApplicationUser? user, string? token)> GenerateEmailConfirmationTokenAsync(string email);
        Task<IdentityResult> ConfirmEmailAsync(ConfirmEmailDto dto);

        Task<(IdentityResult result, ApplicationUser? user, string? token)> GeneratePasswordResetTokenAsync(ForgotPasswordDto dto);
        Task<IdentityResult> ResetPasswordAsync(ResetPasswordDto dto);
    }
}
