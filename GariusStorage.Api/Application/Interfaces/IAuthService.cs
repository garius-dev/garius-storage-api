using GariusStorage.Api.Application.Dtos.Auth;
using GariusStorage.Api.Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GariusStorage.Api.Application.Interfaces
{
    public interface IAuthService
    {
        Task RegisterLocalUserAsync(RegisterRequestDto dto);
        Task FallbackRegisterAndDeleteUserAsync(string userEmail);
        Task<LoginResponseDto> LoginLocalAsync(LoginRequestDto dto);
        Task<ChallengeResult?> ChallengeExternalLoginAsync(string provider, string redirectUrl);
        Task<LoginResponseDto> HandleExternalLoginCallbackAsync();

        Task<(ApplicationUser user, string token)> GenerateEmailConfirmationTokenAsync(string email);
        Task ConfirmEmailAsync(ConfirmEmailDto dto);

        Task<(ApplicationUser user, string token)> GeneratePasswordResetTokenAsync(ForgotPasswordDto dto);
        Task ResetPasswordAsync(ResetPasswordDto dto);
    }
}
