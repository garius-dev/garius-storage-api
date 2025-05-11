using GariusStorage.Api.Application.Dtos;

namespace GariusStorage.Api.Application.Interfaces
{
    public interface ITurnstileService
    {
        Task<TurnstileVerificationResponseDto?> ValidateTokenAsync(string token, string? remoteIp = null);
    }
}
