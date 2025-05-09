using GariusStorage.Api.Application.Dtos.Auth;
using GariusStorage.Api.Application.Interfaces;
using GariusStorage.Api.Domain.Constants;
using GariusStorage.Api.Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;

namespace GariusStorage.Api.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ITokenService _tokenService;
        private readonly ILogger<AuthService> _logger;
        // IEmailService não é injetado aqui, será usado pelo AuthController ou outro serviço de coordenação

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

        public async Task<AuthResult> RegisterLocalUserAsync(RegisterRequestDto dto)
        {
            if (dto.Email != dto.UserName)
            {
                // Padronizando UserName para ser o Email, conforme configuração do Identity.
                _logger.LogInformation("Padronizando UserName para o Email fornecido durante o registro: {Email}", dto.Email);
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
                IsExternalUser = false, // Usuário local
                IsActive = true, // Ou false, se a confirmação de email for obrigatória para ativar
                EmailConfirmed = false, // Requer confirmação de e-mail
                CreatedAt = DateTime.UtcNow,
                LastUpdate = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                _logger.LogWarning("Falha ao registrar usuário {Email}. Erros: {Errors}", dto.Email, string.Join(", ", errors));
                return AuthResult.Failed(errors);
            }

            _logger.LogInformation("Usuário {Email} registrado com sucesso. Aguardando confirmação de e-mail.", user.Email);
            await _userManager.AddToRoleAsync(user, RoleConstants.UserRoleName); // Adiciona à role padrão

            // A lógica de envio de e-mail será tratada pelo AuthController após este método retornar sucesso.

            return AuthResult.Success(new LoginResponseDto { Message = "Registro bem-sucedido. Por favor, verifique seu e-mail para confirmar sua conta." });
        }

        public async Task<AuthResult> LoginLocalAsync(LoginRequestDto dto)
        {
            var user = await _userManager.FindByNameAsync(dto.Username); // Ou FindByEmailAsync se Username for sempre o email
            if (user == null)
            {
                _logger.LogWarning("Tentativa de login falhou para o usuário {Username}: Usuário não encontrado.", dto.Username);
                return AuthResult.Failed("Usuário ou senha inválidos.");
            }

            if (!user.IsActive)
            {
                _logger.LogWarning("Tentativa de login falhou para o usuário {Username}: Conta inativa.", dto.Username);
                return AuthResult.Failed("Esta conta está inativa.");
            }

            if (!user.EmailConfirmed && !user.IsExternalUser) // Somente exige confirmação para usuários locais
            {
                _logger.LogWarning("Tentativa de login falhou para o usuário {Username}: Email não confirmado.", dto.Username);
                return AuthResult.NotAllowed("Por favor, confirme seu endereço de e-mail antes de fazer login.");
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                _logger.LogWarning("Usuário {Username} está bloqueado.", dto.Username);
                return AuthResult.LockedOut();
            }

            if (result.IsNotAllowed) // Pode acontecer por outros motivos além da confirmação de email
            {
                _logger.LogWarning("Login não permitido para o usuário {Username}.", dto.Username);
                return AuthResult.NotAllowed($"Login não permitido para o usuário {dto.Username}.");
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
                Roles = roles,
                Message = "Login bem-sucedido."
            });
        }

        public Task<ChallengeResult?> ChallengeExternalLoginAsync(string provider, string redirectUrl)
        {
            // Configura as propriedades para o provedor de login externo
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Task.FromResult<ChallengeResult?>(new ChallengeResult(provider, properties));
        }

        public async Task<AuthResult> HandleExternalLoginCallbackAsync()
        {
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                _logger.LogError("Não foi possível obter informações do login externo no callback.");
                return AuthResult.Failed("Não foi possível obter informações do login externo. Tente novamente.");
            }

            // Tenta logar o usuário com este provedor externo.
            // O bypassTwoFactor: true é comum aqui porque o 2FA do provedor externo já foi satisfeito.
            var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

            ApplicationUser? userToLogin = null;

            if (signInResult.Succeeded)
            {
                userToLogin = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (userToLogin == null) // Deve ser raro, mas por segurança
                {
                    _logger.LogError("Usuário não encontrado após ExternalLoginSignInAsync bem-sucedido para {LoginProvider} {ProviderKey}", info.LoginProvider, info.ProviderKey);
                    return AuthResult.Failed("Ocorreu um erro ao processar seu login externo.");
                }
                _logger.LogInformation("Usuário {Email} logado com sucesso via {LoginProvider}.", userToLogin.Email, info.LoginProvider);
            }
            else if (signInResult.IsLockedOut)
            {
                _logger.LogWarning("Usuário associado ao login externo {LoginProvider} {ProviderKey} está bloqueado.", info.LoginProvider, info.ProviderKey);
                return AuthResult.LockedOut();
            }
            else // Usuário não tem um login local ou o login externo não está vinculado
            {
                _logger.LogInformation("Usuário não tem login local ou vínculo. Tentando criar ou vincular conta para login externo de {LoginProvider} {ProviderKey}.", info.LoginProvider, info.ProviderKey);

                var email = info.Principal.FindFirstValue(ClaimTypes.Email);
                if (string.IsNullOrEmpty(email))
                {
                    _logger.LogError("Email não encontrado nas claims do provedor externo {LoginProvider}.", info.LoginProvider);
                    return AuthResult.Failed($"Email não fornecido pelo provedor externo '{info.LoginProvider}'. Não é possível criar ou vincular a conta.");
                }

                var existingUser = await _userManager.FindByEmailAsync(email);
                IdentityResult identityResult;

                if (existingUser == null) // Usuário não existe, cria um novo
                {
                    var newUser = new ApplicationUser
                    {
                        UserName = email, // Ou gerar um username único se necessário/desejado
                        Email = email,
                        EmailConfirmed = true, // Provedores externos geralmente já confirmam o email
                        IsExternalUser = true,
                        IsActive = true,
                        FirstName = info.Principal.FindFirstValue(ClaimTypes.GivenName),
                        LastName = info.Principal.FindFirstValue(ClaimTypes.Surname),
                        CreatedAt = DateTime.UtcNow,
                        LastUpdate = DateTime.UtcNow
                    };
                    identityResult = await _userManager.CreateAsync(newUser);
                    if (!identityResult.Succeeded)
                    {
                        _logger.LogError("Falha ao criar novo usuário via login externo {Email}. Erros: {Errors}", email, string.Join(", ", identityResult.Errors.Select(e => e.Description)));
                        return AuthResult.Failed(identityResult.Errors.Select(e => e.Description));
                    }
                    await _userManager.AddToRoleAsync(newUser, RoleConstants.UserRoleName); // Adiciona à role padrão
                    userToLogin = newUser;
                    _logger.LogInformation("Novo usuário {Email} criado via login externo {LoginProvider}.", email, info.LoginProvider);
                }
                else // Usuário já existe com este email
                {
                    if (!existingUser.IsActive)
                    {
                        _logger.LogWarning("Tentativa de login externo para usuário inativo {Email}.", email);
                        return AuthResult.Failed("Esta conta está inativa.");
                    }
                    // Se o usuário existente foi criado localmente, você pode querer impedi-lo de vincular
                    // ou ter uma lógica para mesclar/avisar. Por enquanto, vamos permitir o vínculo.
                    if (!existingUser.IsExternalUser)
                    {
                        _logger.LogInformation("Usuário {Email} existente (local) sendo vinculado a login externo {LoginProvider}.", email, info.LoginProvider);
                        // Opcional: Atualizar IsExternalUser para true ou ter uma flag mista.
                        // existingUser.IsExternalUser = true; // Decida a política aqui
                        // await _userManager.UpdateAsync(existingUser);
                    }
                    userToLogin = existingUser;
                }

                // Vincula o login externo ao usuário (novo ou existente)
                identityResult = await _userManager.AddLoginAsync(userToLogin, info);
                if (!identityResult.Succeeded)
                {
                    _logger.LogError("Falha ao vincular login externo de {LoginProvider} ao usuário {Email}. Erros: {Errors}", info.LoginProvider, userToLogin.Email, string.Join(", ", identityResult.Errors.Select(e => e.Description)));
                    // Não necessariamente um AuthResult.Failed aqui, pois o usuário pode já ter esse login vinculado de uma tentativa anterior que falhou em outra etapa.
                    // Se o erro for 'LoginAlreadyAssociated', podemos prosseguir.
                    if (!identityResult.Errors.Any(e => e.Code == "LoginAlreadyAssociated"))
                    {
                        return AuthResult.Failed(identityResult.Errors.Select(e => e.Description));
                    }
                    _logger.LogInformation("Login externo de {LoginProvider} para o usuário {Email} já estava associado.", info.LoginProvider, userToLogin.Email);
                }
                else
                {
                    _logger.LogInformation("Login externo de {LoginProvider} vinculado com sucesso ao usuário {Email}.", info.LoginProvider, userToLogin.Email);
                }
            }

            if (userToLogin == null) // Se, por algum motivo, userToLogin não foi definido
            {
                _logger.LogError("Falha crítica: userToLogin não foi definido após o fluxo de login externo.");
                return AuthResult.Failed("Ocorreu um erro inesperado durante o login externo.");
            }


            var roles = await _userManager.GetRolesAsync(userToLogin);
            var token = _tokenService.GenerateJwtToken(userToLogin, roles);

            return AuthResult.Success(new LoginResponseDto
            {
                Token = token,
                UserId = userToLogin.Id,
                Username = userToLogin.UserName,
                Email = userToLogin.Email,
                Roles = roles,
                Message = "Login externo bem-sucedido."
            });
        }

        public async Task<(IdentityResult result, ApplicationUser? user, string? token)> GenerateEmailConfirmationTokenAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null || user.IsExternalUser) // Não envia para usuários externos ou inexistentes
            {
                _logger.LogWarning("Tentativa de gerar token de confirmação para email não encontrado ou usuário externo: {Email}", email);
                return (IdentityResult.Failed(new IdentityError { Description = "Usuário não encontrado ou não requer confirmação de e-mail." }), null, null);
            }

            if (user.EmailConfirmed)
            {
                _logger.LogInformation("Email {Email} já está confirmado. Nenhum token de confirmação gerado.", email);
                return (IdentityResult.Success, user, null); // Ou um erro específico
            }

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = HttpUtility.UrlEncode(token); // Importante para URLs
            _logger.LogInformation("Token de confirmação de e-mail gerado para {Email}.", email);
            return (IdentityResult.Success, user, encodedToken);
        }

        public async Task<IdentityResult> ConfirmEmailAsync(ConfirmEmailDto dto)
        {
            if (string.IsNullOrEmpty(dto.UserId) || string.IsNullOrEmpty(dto.Token))
            {
                return IdentityResult.Failed(new IdentityError { Description = "ID do usuário e token são obrigatórios." });
            }

            var user = await _userManager.FindByIdAsync(dto.UserId);
            if (user == null)
            {
                _logger.LogWarning("Tentativa de confirmar email para UserId inválido: {UserId}", dto.UserId);
                return IdentityResult.Failed(new IdentityError { Description = "Usuário não encontrado." });
            }

            if (user.EmailConfirmed)
            {
                _logger.LogInformation("Email {Email} já estava confirmado. Nenhuma ação tomada.", user.Email);
                return IdentityResult.Success; // Ou um erro/mensagem específica
            }

            try
            {
                var decodedToken = HttpUtility.UrlDecode(dto.Token);
                var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

                if (result.Succeeded)
                {
                    user.LastUpdate = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user); // Salva LastUpdate
                    _logger.LogInformation("Email {Email} confirmado com sucesso para o usuário {UserId}.", user.Email, user.Id);
                }
                else
                {
                    _logger.LogWarning("Falha ao confirmar email para {Email}. Erros: {Errors}", user.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
                return result;
            }
            catch (Exception ex) // Captura exceções de decodificação, etc.
            {
                _logger.LogError(ex, "Erro ao decodificar o token de confirmação de e-mail para o usuário {UserId}.", dto.UserId);
                return IdentityResult.Failed(new IdentityError { Description = "Token de confirmação inválido ou malformado." });
            }
        }

        public async Task<(IdentityResult result, ApplicationUser? user, string? token)> GeneratePasswordResetTokenAsync(ForgotPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null || user.IsExternalUser || !user.EmailConfirmed) // Não permite reset para externos ou email não confirmado
            {
                // Não revele se o usuário existe ou não por segurança.
                _logger.LogWarning("Tentativa de reset de senha para email não elegível: {Email}", dto.Email);
                // Retornamos sucesso para não vazar informação se o email existe ou não, mas não enviamos email.
                // O controller pode decidir como lidar com isso (ex: mensagem genérica).
                return (IdentityResult.Success, null, null); // Alterado para não indicar falha diretamente ao chamador interno
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = HttpUtility.UrlEncode(token);
            _logger.LogInformation("Token de reset de senha gerado para {Email}.", dto.Email);
            return (IdentityResult.Success, user, encodedToken);
        }

        public async Task<IdentityResult> ResetPasswordAsync(ResetPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null || user.IsExternalUser)
            {
                // Novamente, não revele a existência do usuário.
                _logger.LogWarning("Tentativa de reset de senha para email não elegível no passo de reset: {Email}", dto.Email);
                return IdentityResult.Failed(new IdentityError { Description = "Falha ao redefinir a senha. Tente novamente." }); // Mensagem genérica
            }

            try
            {
                var decodedToken = HttpUtility.UrlDecode(dto.Token);
                var result = await _userManager.ResetPasswordAsync(user, decodedToken, dto.NewPassword);
                if (result.Succeeded)
                {
                    user.LastUpdate = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);
                    _logger.LogInformation("Senha redefinida com sucesso para {Email}.", dto.Email);
                }
                else
                {
                    _logger.LogWarning("Falha ao redefinir senha para {Email}. Erros: {Errors}", dto.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao decodificar o token de reset de senha para o usuário {Email}.", dto.Email);
                return IdentityResult.Failed(new IdentityError { Description = "Token de redefinição inválido ou malformado." });
            }
        }
    }
}
