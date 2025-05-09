using GariusStorage.Api.Domain.Entities.Identity;
using System.Security.Claims;

namespace GariusStorage.Api.Application.Interfaces
{
    public interface ITokenService
    {
        string GenerateJwtToken(ApplicationUser user, IList<string> roles, IEnumerable<Claim>? additionalClaims = null);
    }
}
