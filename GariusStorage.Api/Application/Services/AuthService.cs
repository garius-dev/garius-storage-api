using GariusStorage.Api.Application.Dtos.Auth;
using GariusStorage.Api.Application.Exceptions;
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
        public async Task FallbackRegisterAndDeleteUserAsync(string userEmail)
        {
            var currentUser = await _userManager.FindByEmailAsync(userEmail);

            if (currentUser == null)
            {
                _logger.LogError($"Usuário {userEmail} não existe e não pode ser deletado");
                //return AuthResult.Failed($"Usuário {userEmail} não existe e não pode ser deletado");
                throw new NotFoundException($"Usuário com email '{userEmail}' não encontrado para deleção no fallback.", "USER_NOT_FOUND_FOR_FALLBACK_DELETION");
            }

            var fallbackResult = await _userManager.DeleteAsync(currentUser);
            if (!fallbackResult.Succeeded)
            {
                var errors = fallbackResult.Errors.ToDictionary(e => e.Code, e => new[] { e.Description });
                _logger.LogWarning("Falha ao deletar usuário {UserEmail} no fallback. Erros: {Errors}", userEmail, string.Join(", ", fallbackResult.Errors.Select(e => e.Description)));
                throw new OperationFailedException($"Falha ao reverter o registro do usuário '{userEmail}'.", "USER_DELETION_FAILED_FALLBACK", errors);
            }

            _logger.LogInformation("Usuário {UserEmail} deletado com sucesso no fallback.", userEmail);
        }

        public async Task RegisterLocalUserAsync(RegisterRequestDto dto)
        {
            if (dto.Email != dto.UserName)
            {
                _logger.LogInformation("Padronizando UserName para o Email fornecido durante o registro: {Email}", dto.Email);
                dto.UserName = dto.Email;
            }

            var existingUserByEmail = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUserByEmail != null)
            {
                _logger.LogWarning("Tentativa de registro com email já existente: {Email}", dto.Email);
                throw new ConflictException("Este email já está registrado.", "EMAIL_EXISTS");
            }

            var user = new ApplicationUser
            {
                UserName = dto.UserName,
                Email = dto.Email,
                IsExternalUser = false,
                IsActive = true,
                EmailConfirmed = false,
                CreatedAt = DateTime.UtcNow,
                LastUpdate = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded)
            {
                var errors = result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description });
                _logger.LogWarning("Falha ao registrar usuário {Email}. Erros: {Errors}", dto.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
                throw new ValidationException(errors);
            }

            _logger.LogInformation("Usuário {Email} registrado com sucesso. Aguardando confirmação de e-mail.", user.Email);
            var roleResult = await _userManager.AddToRoleAsync(user, RoleConstants.UserRoleName);
            if (!roleResult.Succeeded)
            {
                _logger.LogError("Falha ao adicionar role '{UserRoleName}' ao usuário {Email} recém-registrado. Erros: {Errors}",
                   RoleConstants.UserRoleName, user.Email, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                throw new OperationFailedException($"Falha ao atribuir role padrão ao usuário '{user.Email}'.", "ROLE_ASSIGNMENT_FAILED", roleResult.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
            }
        }

        public async Task<LoginResponseDto> LoginLocalAsync(LoginRequestDto dto)
        {
            var user = await _userManager.FindByNameAsync(dto.Username);
            if (user == null)
            {
                _logger.LogWarning("Tentativa de login falhou para o usuário {Username}: Usuário não encontrado.", dto.Username);
                throw new PermissionDeniedException("Usuário ou senha inválidos.", "INVALID_CREDENTIALS");
            }

            if(user.Email == null || user.UserName == null)
            {
                _logger.LogWarning("Tentativa de login falhou para o usuário {Username}: Email ou nome de usuário inválidos.", dto.Username);
                throw new PermissionDeniedException("Usuário ou senha inválidos.", "INVALID_CREDENTIALS");
            }

            if (!user.IsActive)
            {
                _logger.LogWarning("Tentativa de login falhou para o usuário {Username}: Conta inativa.", dto.Username);
                throw new PermissionDeniedException("Esta conta está inativa.", "ACCOUNT_INACTIVE");
            }

            if (!user.EmailConfirmed && !user.IsExternalUser)
            {
                _logger.LogWarning("Tentativa de login falhou para o usuário {Username}: Email não confirmado.", dto.Username);
                throw new PermissionDeniedException("Por favor, confirme seu endereço de e-mail antes de fazer login.", "EMAIL_NOT_CONFIRMED");
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                _logger.LogWarning("Usuário {Username} está bloqueado.", dto.Username);
                throw new PermissionDeniedException("Conta bloqueada devido a múltiplas tentativas de login falhas.", "ACCOUNT_LOCKED_OUT");
            }

            if (result.IsNotAllowed)
            {
                _logger.LogWarning("Login não permitido para o usuário {Username}.", dto.Username);
                throw new PermissionDeniedException($"Login não permitido para o usuário {dto.Username}.", "LOGIN_NOT_ALLOWED");
            }

            if (result.RequiresTwoFactor)
            {
                _logger.LogInformation("Login para {Username} requer autenticação de dois fatores.", dto.Username);
                throw new PermissionDeniedException("Autenticação de dois fatores é necessária.", "2FA_REQUIRED");
            }

            if (!result.Succeeded)
            {
                _logger.LogWarning("Tentativa de login falhou para o usuário {Username}: Senha inválida.", dto.Username);
                throw new PermissionDeniedException("Usuário ou senha inválidos.", "INVALID_CREDENTIALS");
            }

            _logger.LogInformation("Usuário {Username} logado com sucesso.", dto.Username);
            var roles = await _userManager.GetRolesAsync(user);
            var token = _tokenService.GenerateJwtToken(user, roles);

            return new LoginResponseDto
            {
                Token = token,
                UserId = user.Id,
                Username = user.UserName,
                Email = user.Email,
                Roles = roles,
                Message = "Login bem-sucedido."
            };
        }

        public Task<ChallengeResult?> ChallengeExternalLoginAsync(string provider, string redirectUrl)
        {
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Task.FromResult<ChallengeResult?>(new ChallengeResult(provider, properties));
        }

        public async Task<LoginResponseDto> HandleExternalLoginCallbackAsync()
        {
            var info = await _signInManager.GetExternalLoginInfoAsync();

            if (info == null)
            {
                _logger.LogError("Não foi possível obter informações do login externo no callback.");
                throw new OperationFailedException("Não foi possível obter informações do login externo. Tente novamente.", "EXTERNAL_LOGIN_INFO_ERROR");
            }

            var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
            ApplicationUser? userToLogin;

            if (signInResult.Succeeded)
            {
                userToLogin = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (userToLogin == null)
                {
                    _logger.LogError("Usuário não encontrado após ExternalLoginSignInAsync bem-sucedido para {LoginProvider} {ProviderKey}", info.LoginProvider, info.ProviderKey);
                    throw new OperationFailedException("Ocorreu um erro ao processar seu login externo.", "EXTERNAL_LOGIN_USER_NOT_FOUND_POST_SIGNIN");
                }
                _logger.LogInformation("Usuário {Email} logado com sucesso via {LoginProvider}.", userToLogin.Email, info.LoginProvider);
            }
            else if (signInResult.IsLockedOut)
            {
                _logger.LogWarning("Usuário associado ao login externo {LoginProvider} {ProviderKey} está bloqueado.", info.LoginProvider, info.ProviderKey);
                throw new PermissionDeniedException("Conta bloqueada.", "ACCOUNT_LOCKED_OUT");
            }
            else
            {
                _logger.LogInformation("Usuário não tem login local ou vínculo. Tentando criar ou vincular conta para login externo de {LoginProvider} {ProviderKey}.", info.LoginProvider, info.ProviderKey);
                var email = info.Principal.FindFirstValue(ClaimTypes.Email);
                if (string.IsNullOrEmpty(email))
                {
                    _logger.LogError("Email não encontrado nas claims do provedor externo {LoginProvider}.", info.LoginProvider);
                    throw new ValidationException("Email não fornecido pelo provedor externo. Não é possível criar ou vincular a conta.", "EXTERNAL_LOGIN_NO_EMAIL");
                }

                var existingUser = await _userManager.FindByEmailAsync(email);
                IdentityResult identityResult;

                if (existingUser == null)
                {
                    var newUser = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true,
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
                        throw new ValidationException(identityResult.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
                    }

                    var roleResult = await _userManager.AddToRoleAsync(newUser, RoleConstants.UserRoleName);

                    if (!roleResult.Succeeded)
                    {
                        _logger.LogError("Falha ao adicionar role padrão ao novo usuário externo {Email}. Erros: {Errors}", newUser.Email, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                        throw new OperationFailedException($"Falha ao atribuir role padrão ao usuário externo '{newUser.Email}'.", "EXTERNAL_USER_ROLE_ASSIGNMENT_FAILED", roleResult.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
                    }

                    userToLogin = newUser;

                    _logger.LogInformation("Novo usuário {Email} criado via login externo {LoginProvider}.", email, info.LoginProvider);
                }
                else
                {
                    if (!existingUser.IsActive)
                    {
                        _logger.LogWarning("Tentativa de login externo para usuário inativo {Email}.", email);
                        throw new PermissionDeniedException("Esta conta está inativa.", "ACCOUNT_INACTIVE");
                    }

                    userToLogin = existingUser;
                }

                identityResult = await _userManager.AddLoginAsync(userToLogin, info);

                if (!identityResult.Succeeded && !identityResult.Errors.Any(e => e.Code == "LoginAlreadyAssociated"))
                {
                    _logger.LogError("Falha ao vincular login externo de {LoginProvider} ao usuário {Email}. Erros: {Errors}", info.LoginProvider, userToLogin.Email, string.Join(", ", identityResult.Errors.Select(e => e.Description)));
                    throw new OperationFailedException("Falha ao vincular login externo.", "EXTERNAL_LOGIN_LINK_FAILED", identityResult.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
                }

                _logger.LogInformation("Login externo de {LoginProvider} vinculado com sucesso (ou já estava vinculado) ao usuário {Email}.", info.LoginProvider, userToLogin.Email);
            }

            if (userToLogin == null)
            {
                _logger.LogError("Falha crítica: userToLogin não foi definido após o fluxo de login externo.");
                throw new OperationFailedException("Ocorreu um erro inesperado durante o login externo.", "EXTERNAL_LOGIN_UNEXPECTED_STATE");
            }

            var roles = await _userManager.GetRolesAsync(userToLogin);
            var token = _tokenService.GenerateJwtToken(userToLogin, roles);

            return new LoginResponseDto
            {
                Token = token,
                UserId = userToLogin.Id,
                Username = userToLogin.UserName,
                Email = userToLogin.Email,
                Roles = roles,
                Message = "Login externo bem-sucedido."
            };
        }

        public async Task<(ApplicationUser user, string token)> GenerateEmailConfirmationTokenAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                _logger.LogWarning("Tentativa de gerar token de confirmação para email não encontrado: {Email}", email);
                throw new NotFoundException($"Usuário com email '{email}' não encontrado.", "USER_NOT_FOUND");
            }
            if (user.IsExternalUser)
            {
                _logger.LogInformation("Usuário externo {Email} não requer confirmação de e-mail local.", email);
                throw new ValidationException("Usuários externos não requerem confirmação de e-mail por este método.", "EXTERNAL_USER_NO_CONFIRMATION_NEEDED");
            }
            if (user.EmailConfirmed)
            {
                _logger.LogInformation("Email {Email} já está confirmado. Nenhum token de confirmação gerado.", email);
                throw new ValidationException("Este email já foi confirmado.", "EMAIL_ALREADY_CONFIRMED");
            }

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = HttpUtility.UrlEncode(token);
            _logger.LogInformation("Token de confirmação de e-mail gerado para {Email}.", email);
            return (user, encodedToken);
        }

        public async Task ConfirmEmailAsync(ConfirmEmailDto dto)
        {
            if (string.IsNullOrEmpty(dto.UserId) || string.IsNullOrEmpty(dto.Token))
            {
                // Usando o construtor ValidationException(dictionary, message, errorCode)
                throw new ValidationException(
                    new Dictionary<string, string[]> {
                        { nameof(dto.UserId), new[]{"O ID do usuário é obrigatório."} },
                        { nameof(dto.Token), new[]{"O token de confirmação é obrigatório."} }
                    },
                    "Dados para confirmação de e-mail incompletos.",
                    "CONFIRM_EMAIL_DATA_INCOMPLETE"
                );
            }

            var user = await _userManager.FindByIdAsync(dto.UserId);
            if (user == null)
            {
                _logger.LogWarning("Tentativa de confirmar email para UserId inválido: {UserId}", dto.UserId);
                throw new NotFoundException($"Usuário com ID '{dto.UserId}' não encontrado.", "USER_NOT_FOUND");
            }

            if (user.EmailConfirmed)
            {
                _logger.LogInformation("Email {Email} já estava confirmado. Nenhuma ação tomada.", user.Email);
                return;
            }

            try
            {
                var decodedToken = HttpUtility.UrlDecode(dto.Token);
                var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

                if (!result.Succeeded)
                {
                    _logger.LogWarning("Falha ao confirmar email para {Email}. Erros: {Errors}", user.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
                    throw new ValidationException(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }), "Falha ao validar o token de confirmação de e-mail.", "EMAIL_CONFIRMATION_TOKEN_INVALID");
                }

                user.LastUpdate = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
                _logger.LogInformation("Email {Email} confirmado com sucesso para o usuário {UserId}.", user.Email, user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao decodificar o token de confirmação de e-mail para o usuário {UserId}.", dto.UserId);
                throw new ValidationException("Token de confirmação inválido ou malformado.", "INVALID_CONFIRMATION_TOKEN_FORMAT");
            }
        }

        public async Task<(ApplicationUser user, string token)> GeneratePasswordResetTokenAsync(ForgotPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
            {
                _logger.LogWarning("Tentativa de reset de senha para email não encontrado: {Email}", dto.Email);
                throw new NotFoundException($"Nenhuma conta encontrada para o email '{dto.Email}'.", "USER_NOT_FOUND_FOR_RESET");
            }
            if (user.IsExternalUser)
            {
                _logger.LogWarning("Tentativa de reset de senha para usuário externo: {Email}", dto.Email);
                throw new ValidationException("Não é possível redefinir a senha para contas externas por este método.", "EXTERNAL_USER_NO_PASSWORD_RESET");
            }
            if (!user.EmailConfirmed)
            {
                _logger.LogWarning("Tentativa de reset de senha para email não confirmado: {Email}", dto.Email);
                throw new ValidationException("Confirme seu endereço de e-mail antes de redefinir a senha.", "EMAIL_NOT_CONFIRMED_FOR_RESET");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);       
            var encodedToken = HttpUtility.UrlEncode(token);

            _logger.LogInformation("Token de reset de senha gerado para {Email}.", dto.Email);

            if(user == null || string.IsNullOrEmpty(token))
            {
                _logger.LogWarning($"Usuário ou token de reset não são válidos: User = {(user == null ? "NULL" : "VALID")} e Token = {token}");
                throw new PermissionDeniedException("Usuário ou token de reset não são válidos", "USER_OR_TOKEN_NOT_VALID");
            }

            return (user, encodedToken);
        }

        public async Task ResetPasswordAsync(ResetPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
            {
                _logger.LogWarning("Tentativa de reset de senha para email não encontrado no passo de reset: {Email}", dto.Email);
                throw new ValidationException("Falha ao redefinir a senha. O link pode ser inválido ou ter expirado.", "PASSWORD_RESET_FAILED");
            }
            if (user.IsExternalUser)
            {
                _logger.LogWarning("Tentativa de reset de senha para usuário externo no passo de reset: {Email}", dto.Email);
                throw new ValidationException("Não é possível redefinir a senha para contas externas.", "EXTERNAL_USER_NO_PASSWORD_RESET");
            }

            try
            {
                var decodedToken = HttpUtility.UrlDecode(dto.Token);
                var result = await _userManager.ResetPasswordAsync(user, decodedToken, dto.NewPassword);
                if (!result.Succeeded)
                {
                    _logger.LogWarning("Falha ao redefinir senha para {Email}. Erros: {Errors}", dto.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
                    throw new ValidationException(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }), "Falha na validação ao redefinir senha.", "PASSWORD_RESET_VALIDATION_FAILED");
                }

                user.LastUpdate = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
                _logger.LogInformation("Senha redefinida com sucesso para {Email}.", dto.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao decodificar o token de reset de senha para o usuário {Email}.", dto.Email);
                throw new ValidationException("Token de redefinição inválido ou malformado.", "INVALID_RESET_TOKEN_FORMAT");
            }
        }
    }
}
