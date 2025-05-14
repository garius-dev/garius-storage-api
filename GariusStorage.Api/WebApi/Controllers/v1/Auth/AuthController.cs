using Asp.Versioning;
using GariusStorage.Api.Application.Dtos.Auth;
using GariusStorage.Api.Application.Interfaces; // Para IAuthService, IEmailService, ITurnstileService
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.WebUtilities;
using GariusStorage.Api.Configuration;
using Microsoft.Extensions.Options;
using GariusStorage.Api.Extensions;
using GariusStorage.Api.Application.Exceptions;

namespace GariusStorage.Api.WebApi.Controllers.v1.Auth
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/users")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IEmailService _emailService;
        private readonly ITurnstileService _turnstileService;
        private readonly ILogger<AuthController> _logger;
        private readonly UrlCallbackSettings _urlCallbackSettings;

        public AuthController(
            IAuthService authService,
            IEmailService emailService,
            ITurnstileService turnstileService,
            ILogger<AuthController> logger,
            IOptions<UrlCallbackSettings> urlCallbackSettings)
        {
            _authService = authService;
            _emailService = emailService;
            _turnstileService = turnstileService;
            _logger = logger;
            _urlCallbackSettings = urlCallbackSettings.Value;

            if(!string.IsNullOrEmpty(_urlCallbackSettings.Environment) && _urlCallbackSettings.UrlCallbacks != null && _urlCallbackSettings.UrlCallbacks.Any())
            {
                _urlCallbackSettings.UrlCallbacks.FilterKeysStartingWith(_urlCallbackSettings.Environment);
            }
        }

        [HttpPost("/api/v{version:apiVersion}/auth/register")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterLocalUser([FromBody] RegisterRequestDto registerDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Validar o token do Turnstile primeiro
            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var turnstileResult = await _turnstileService.ValidateTokenAsync(registerDto.TurnstileToken, remoteIp);

            if (turnstileResult == null || !turnstileResult.Success)
            {
                _logger.LogWarning("Validação do Turnstile falhou para tentativa de registro. Erros: {ErrorCodes}",
                     string.Join(", ", turnstileResult?.ErrorCodes ?? new List<string> { "unknown" }));

                throw new ValidationException("Falha na verificação de segurança (CAPTCHA). Por favor, tente novamente.", "CAPTCHA_VALIDATION_FAILED");
            }

            _logger.LogInformation("Turnstile validado com sucesso para registro. Hostname: {Hostname}", turnstileResult.Hostname);

            await _authService.RegisterLocalUserAsync(registerDto);

            try
            {
                var (user, token) = await _authService.GenerateEmailConfirmationTokenAsync(registerDto.Email);
                var frontendConfirmEmailUrl = _urlCallbackSettings.GetValueByKey("ConfirmEmailUrl") ?? 
                    throw new OperationFailedException("URL de retorno da confirmação de email não foi encontrada", "URL_CALLBACK_NOT_FOUND");

                var callbackUrl = $"{frontendConfirmEmailUrl}?userId={user.Id}&token={token}";
                var emailSent = await _emailService.SendEmailConfirmationLinkAsync(user.Email, user.UserName, callbackUrl);
                if (!emailSent)
                {
                    _logger.LogError("Falha ao enviar o e-mail de confirmação para {Email}. Iniciando fallback para deletar usuário.", registerDto.Email);
                    await _authService.FallbackRegisterAndDeleteUserAsync(registerDto.Email);
                    throw new OperationFailedException("Seu registro foi processado, mas houve uma falha ao enviar o e-mail de confirmação. Por favor, tente se registrar novamente.", "EMAIL_CONFIRMATION_SEND_FAILED_ROLLEDBACK");
                }
            }
            catch (NotFoundException ex)
            {
                _logger.LogError(ex, "Erro ao gerar token de confirmação para usuário recém-registrado {Email}. Isso não deveria acontecer.", registerDto.Email);
                throw new OperationFailedException("Ocorreu um erro ao finalizar seu registro. Por favor, contate o suporte.", "REGISTRATION_FINALIZATION_ERROR_TOKEN_GEN", null, ex);
            }

            return Ok(new { Message = "Registro bem-sucedido. Por favor, verifique seu e-mail para confirmar sua conta." });
        }

        [HttpPost("/api/v{version:apiVersion}/auth/login")]
        [AllowAnonymous]
        public async Task<IActionResult> LoginLocal([FromBody] LoginRequestDto loginDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Validar o token do Turnstile primeiro
            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var turnstileResult = await _turnstileService.ValidateTokenAsync(loginDto.TurnstileToken, remoteIp);

            if (turnstileResult == null || !turnstileResult.Success)
            {
                _logger.LogWarning("Validação do Turnstile falhou para tentativa de login. Erros: {ErrorCodes}",
                    string.Join(", ", turnstileResult?.ErrorCodes ?? new List<string> { "unknown" }));
                throw new ValidationException("Falha na verificação de segurança (CAPTCHA). Por favor, tente novamente.", "CAPTCHA_VALIDATION_FAILED");
            }

            _logger.LogInformation("Turnstile validado com sucesso para login. Hostname: {Hostname}", turnstileResult.Hostname);

            var loginResponse = await _authService.LoginLocalAsync(loginDto);
            return Ok(loginResponse);
        }


        [HttpGet("/api/v{version:apiVersion}/auth/external-login-challenge")]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginChallenge([FromQuery] string provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                throw new ValidationException("O nome do provedor externo é obrigatório.", "PROVIDER_NAME_REQUIRED");
            }

            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Auth", values: null, protocol: Request.Scheme);
            _logger.LogInformation("Gerando desafio para provedor {Provider}. O SignInManager usará o CallbackPath configurado.", provider);

            var challenge = await _authService.ChallengeExternalLoginAsync(provider, redirectUrl!); // ChallengeExternalLoginAsync retorna ChallengeResult diretamente

            if (challenge == null) // Embora o método no serviço retorne Task<ChallengeResult>, pode ser null se algo der errado internamente antes do Challenge
            {
                throw new OperationFailedException($"Provedor externo '{provider}' não suportado ou erro na configuração.", "EXTERNAL_PROVIDER_CHALLENGE_ERROR");
            }
            return challenge;
        }

        [HttpGet("/api/v{version:apiVersion}/auth/external-login-callback")]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback()
        {
            _logger.LogInformation("Recebido callback do login externo.");

            var loginResponse = await _authService.HandleExternalLoginCallbackAsync();

            var frontendRedirectUrl = _urlCallbackSettings.GetValueByKey("ExternalLoginReturnUrl") ??
                throw new OperationFailedException("URL de retorno do login externo não foi encontrada", "URL_CALLBACK_NOT_FOUND");

            var queryParams = new Dictionary<string, string?>
            {
                { "token", loginResponse.Token },
                { "userId", loginResponse.UserId.ToString() },
                { "username", loginResponse.Username },
                { "email", loginResponse.Email }
                // Adicionar roles aqui se o frontend precisar delas imediatamente no fragmento
                // { "roles", string.Join(",", loginResponse.Roles) } 
            };

            var fragmentString = string.Join("&", queryParams
                .Where(kvp => kvp.Value != null)
                .Select(kvp => $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}"));

            var successUri = $"{frontendRedirectUrl}#{fragmentString}";

            _logger.LogInformation("Redirecionando para o frontend após login externo bem-sucedido: {SuccessUri}", successUri);
            return Redirect(successUri);
        }

        [HttpPost("/api/v{version:apiVersion}/auth/confirm-email-request")]
        [AllowAnonymous]
        public async Task<IActionResult> RequestEmailConfirmation([FromBody] ForgotPasswordDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var (user, token) = await _authService.GenerateEmailConfirmationTokenAsync(dto.Email);
                // Se chegou aqui, o usuário é elegível e o token foi gerado.

                var frontendConfirmEmailUrl = _urlCallbackSettings.GetValueByKey("ConfirmEmailUrl") ??
                    throw new OperationFailedException("URL de retorno da confirmação de email não foi encontrada", "URL_CALLBACK_NOT_FOUND");

                var callbackUrl = $"{frontendConfirmEmailUrl}?userId={user.Id}&token={token}";
                await _emailService.SendEmailConfirmationLinkAsync(user.Email, user.UserName, callbackUrl);
            }
            catch (NotFoundException ex)
            {
                _logger.LogWarning(ex, "Solicitação de confirmação de e-mail para usuário não encontrado ou não elegível: {Email}", dto.Email);
            }
            catch (ValidationException ex)
            {
                _logger.LogInformation(ex, "Solicitação de confirmação de e-mail para {Email}, mas não aplicável: {Message}", dto.Email, ex.Message);
            }

            return Ok(new { Message = "Se uma conta correspondente ao seu e-mail existir e precisar de confirmação, um link foi enviado." });
        }

        [HttpPost("/api/v{version:apiVersion}/auth/confirm-email")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailDto confirmEmailDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // AuthService.ConfirmEmailAsync agora retorna Task (void) ou lança exceção
            await _authService.ConfirmEmailAsync(confirmEmailDto);
            return Ok(new { Message = "E-mail confirmado com sucesso. Você já pode fazer login." });
        }

        [HttpPost("/api/v{version:apiVersion}/auth/forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var (user, token) = await _authService.GeneratePasswordResetTokenAsync(forgotPasswordDto);
                // Se chegou aqui, o usuário é elegível e o token foi gerado.

                var frontendResetPasswordUrl = _urlCallbackSettings.GetValueByKey("ResetPasswordUrl") ??
                    throw new OperationFailedException("URL de retorno do reset de password não foi encontrada", "URL_CALLBACK_NOT_FOUND");

                var callbackUrl = $"{frontendResetPasswordUrl}?email={HttpUtility.UrlEncode(user.Email)}&token={token}";
                await _emailService.SendPasswordResetLinkAsync(user.Email, user.UserName, callbackUrl);
            }
            catch (NotFoundException ex)
            {
                _logger.LogWarning(ex, "Solicitação de redefinição de senha para usuário não encontrado ou não elegível: {Email}", forgotPasswordDto.Email);
            }
            catch (ValidationException ex)
            {
                _logger.LogInformation(ex, "Solicitação de redefinição de senha para {Email}, mas não aplicável: {Message}", forgotPasswordDto.Email, ex.Message);
            }

            return Ok(new { Message = "Se sua conta existir e for elegível, um e-mail foi enviado com instruções para redefinir sua senha." });
        }

        [HttpPost("/api/v{version:apiVersion}/auth/reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // AuthService.ResetPasswordAsync agora retorna Task (void) ou lança exceção
            await _authService.ResetPasswordAsync(resetPasswordDto);
            return Ok(new { Message = "Senha redefinida com sucesso." });
        }
    }
}
