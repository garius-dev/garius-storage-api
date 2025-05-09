using GariusStorage.Api.Application.Dtos.Auth;
using GariusStorage.Api.Application.Interfaces;
using GariusStorage.Api.Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace GariusStorage.Api.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ITokenService _tokenService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ITokenService tokenService,
            ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<AuthResult> RegisterAsync(RegisterRequestDto dto)
        {
            if (dto.Email != dto.UserName)
            {
                // Padronizando UserName para ser o Email, conforme configuração do Identity.
                dto.UserName = dto.Email;
            }

            var existingUserByEmail = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUserByEmail != null)
            {
                _logger.LogWarning("Tentativa de registro com email já existente: {Email}", dto.Email);
                return AuthResult.Failed("Este email já está registrado.");
            }

            var user = new ApplicationUser
            {
                UserName = dto.UserName,
                Email = dto.Email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                _logger.LogWarning("Falha ao registrar usuário {Email}. Erros: {Errors}", dto.Email, string.Join(", ", errors));
                return AuthResult.Failed(errors);
            }

            _logger.LogInformation("Usuário {Email} registrado com sucesso.", user.Email);
            // Opcional: Adicionar a uma role padrão
            await _userManager.AddToRoleAsync(user, "User");

            // Não retorna token no registro por padrão, o usuário deve fazer login.
            // Se quiser retornar token, chame a lógica de geração de token aqui.
            return AuthResult.Success(new LoginResponseDto { Message = "Registro bem-sucedido. Por favor, faça login." }); // Custom LoginResponseDto for just message
        }

        public async Task<AuthResult> LoginAsync(LoginRequestDto dto)
        {
            var user = await _userManager.FindByNameAsync(dto.Username); // Ou FindByEmailAsync
            if (user == null)
            {
                _logger.LogWarning("Tentativa de login falhou para o usuário {Username}: Usuário não encontrado.", dto.Username);
                return AuthResult.Failed("Usuário ou senha inválidos.");
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                _logger.LogWarning("Usuário {Username} está bloqueado.", dto.Username);
                return AuthResult.LockedOut();
            }

            if (result.IsNotAllowed)
            {
                _logger.LogWarning("Login não permitido para o usuário {Username}. (Ex: Email não confirmado)", dto.Username);
                return AuthResult.NotAllowed();
            }

            if (result.RequiresTwoFactor)
            {
                _logger.LogInformation("Login para {Username} requer autenticação de dois fatores.", dto.Username);
                return AuthResult.RequiresTwoFactorAuth(); // Lidar com 2FA no controller/frontend
            }

            if (!result.Succeeded)
            {
                _logger.LogWarning("Tentativa de login falhou para o usuário {Username}: Senha inválida.", dto.Username);
                return AuthResult.Failed("Usuário ou senha inválidos.");
            }

            _logger.LogInformation("Usuário {Username} logado com sucesso.", dto.Username);
            var roles = await _userManager.GetRolesAsync(user);
            var token = _tokenService.GenerateJwtToken(user, roles);

            return AuthResult.Success(new LoginResponseDto
            {
                Token = token,
                UserId = user.Id,
                Username = user.UserName,
                Email = user.Email,
                Roles = roles
            });
        }

        public async Task<AuthResult> HandleExternalLoginCallbackAsync()
        {
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                _logger.LogError("Não foi possível obter informações do login externo no callback.");
                return AuthResult.Failed("Não foi possível obter informações do login externo.");
            }

            // Tenta logar o usuário com este provedor externo.
            var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

            if (signInResult.Succeeded)
            {
                var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (user == null) // Deve ser raro
                {
                    _logger.LogError("Usuário não encontrado após ExternalLoginSignInAsync bem-sucedido para {LoginProvider} {ProviderKey}", info.LoginProvider, info.ProviderKey);
                    return AuthResult.Failed("Usuário não encontrado após login externo bem-sucedido.");
                }
                _logger.LogInformation("Usuário {Email} logado com sucesso via {LoginProvider}.", user.Email, info.LoginProvider);
                var roles = await _userManager.GetRolesAsync(user);
                var token = _tokenService.GenerateJwtToken(user, roles);
                return AuthResult.Success(new LoginResponseDto
                {
                    Token = token,
                    UserId = user.Id,
                    Username = user.UserName,
                    Email = user.Email,
                    Roles = roles
                });
            }

            if (signInResult.IsLockedOut)
            {
                _logger.LogWarning("Usuário associado ao login externo {LoginProvider} {ProviderKey} está bloqueado.", info.LoginProvider, info.ProviderKey);
                return AuthResult.LockedOut();
            }

            if (signInResult.RequiresTwoFactor)
            {
                _logger.LogInformation("Login externo para {LoginProvider} {ProviderKey} requer autenticação de dois fatores.", info.LoginProvider, info.ProviderKey);
                return AuthResult.RequiresTwoFactorAuth();
            }

            // Se o usuário não tem um login local (primeira vez com este provedor externo)
            _logger.LogInformation("Usuário não tem login local. Tentando criar ou vincular conta para login externo de {LoginProvider} {ProviderKey}.", info.LoginProvider, info.ProviderKey);

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email))
            {
                _logger.LogError("Email não encontrado nas claims do provedor externo {LoginProvider}.", info.LoginProvider);
                return AuthResult.Failed($"Email não fornecido pelo provedor externo '{info.LoginProvider}'.");
            }

            var existingUser = await _userManager.FindByEmailAsync(email);
            IdentityResult identityResult;

            if (existingUser == null) // Usuário não existe, cria um novo
            {
                var newUser = new ApplicationUser
                {
                    UserName = email, // Ou gerar um username único se necessário/desejado
                    Email = email,
                    EmailConfirmed = true // Provedores externos geralmente já confirmam o email
                    // Adicionar outros campos como Nome, Sobrenome se vierem do provedor
                    // FullName = info.Principal.FindFirstValue(ClaimTypes.Name),
                };
                identityResult = await _userManager.CreateAsync(newUser);
                if (!identityResult.Succeeded)
                {
                    _logger.LogError("Falha ao criar novo usuário via login externo {Email}. Erros: {Errors}", email, string.Join(", ", identityResult.Errors.Select(e => e.Description)));
                    return AuthResult.Failed(identityResult.Errors.Select(e => e.Description));
                }
                existingUser = newUser; // Atribui o novo usuário para a próxima etapa
            }
            else // Usuário já existe com este email, apenas vincula o login externo
            {
                _logger.LogInformation("Usuário com email {Email} já existe. Vinculando login externo de {LoginProvider}.", email, info.LoginProvider);
            }


            identityResult = await _userManager.AddLoginAsync(existingUser, info);
            if (!identityResult.Succeeded)
            {
                _logger.LogError("Falha ao vincular login externo de {LoginProvider} ao usuário {Email}. Erros: {Errors}", info.LoginProvider, email, string.Join(", ", identityResult.Errors.Select(e => e.Description)));
                return AuthResult.Failed(identityResult.Errors.Select(e => e.Description));
            }

            _logger.LogInformation("Login externo de {LoginProvider} vinculado/criado para o usuário {Email}.", info.LoginProvider, email);

            // Loga o usuário na sessão de cookies temporariamente para que o GetRolesAsync funcione corretamente.
            // Embora estejamos usando JWT, o SignInManager pode depender de um cookie temporário para algumas operações internas
            // após um login externo bem-sucedido antes de emitirmos nosso próprio token.
            await _signInManager.SignInAsync(existingUser, isPersistent: false);

            var rolesAfterLink = await _userManager.GetRolesAsync(existingUser);
            var tokenAfterLink = _tokenService.GenerateJwtToken(existingUser, rolesAfterLink);

            return AuthResult.Success(new LoginResponseDto
            {
                Token = tokenAfterLink,
                UserId = existingUser.Id,
                Username = existingUser.UserName,
                Email = existingUser.Email,
                Roles = rolesAfterLink
            });
        }
    }
}
