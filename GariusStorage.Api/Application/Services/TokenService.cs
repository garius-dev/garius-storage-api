using GariusStorage.Api.Application.Interfaces;
using GariusStorage.Api.Configuration;
using GariusStorage.Api.Domain.Entities.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace GariusStorage.Api.Application.Services
{
    public class TokenService : ITokenService
    {
        private readonly JwtSettings _jwtSettings;
        private readonly SymmetricSecurityKey _key;

        public TokenService(IOptions<JwtSettings> jwtOptions)
        {
            _jwtSettings = jwtOptions.Value;
            _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        }

        public string GenerateJwtToken(ApplicationUser user, IList<string> roles, IEnumerable<Claim>? additionalClaims = null)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim("is_company_owner", user.IsCompanyOwner ? "yes" : "no")
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // Adicionar CompanyId como claim, se existir
            if (user.CompanyId.HasValue && user.CompanyId.Value != Guid.Empty)
            {
                claims.Add(new Claim("company_id", user.CompanyId.Value.ToString()));
            }
            else
            {
                claims.Add(new Claim("company_id", "none"));
            }

            if (additionalClaims != null)
            {
                claims.AddRange(additionalClaims);
            }

            var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256Signature);
            var expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationInMinutes);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expires,
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = creds
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }
    }
}
