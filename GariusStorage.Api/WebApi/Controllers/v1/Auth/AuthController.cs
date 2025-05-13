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
                return BadRequest(AuthResult.Failed("Falha na verificação de segurança (CAPTCHA). Por favor, tente novamente."));
            }

            _logger.LogInformation("Turnstile validado com sucesso para registro. Hostname: {Hostname}", turnstileResult.Hostname);

            var authOperationResult = await _authService.RegisterLocalUserAsync(registerDto);
            if (!authOperationResult.Succeeded) return BadRequest(authOperationResult);

            var (identityResult, user, token) = await _authService.GenerateEmailConfirmationTokenAsync(registerDto.Email);
            if (identityResult.Succeeded && user != null && token != null)
            {
                var frontendConfirmEmailUrl = _urlCallbackSettings.GetValueByKey("ConfirmEmailUrl");
                if (!string.IsNullOrWhiteSpace(frontendConfirmEmailUrl))
                {
                    var callbackUrl = $"{frontendConfirmEmailUrl}?userId={user.Id}&token={token}";
                    var emailResponse = await _emailService.SendEmailConfirmationLinkAsync(user.Email, user.UserName, callbackUrl);
                    if (!emailResponse)
                    {
                        await _authService.FallBackRegister(user.Email);
                        _logger.LogError("Falha ao enviar o e-mail de confirmação. Usuário foi deletado");
                        return BadRequest(new { Message = "Falha ao enviar o e-mail de confirmação. O usuário foi deletado." });
                    }
                }
                else _logger.LogError("ConfirmEmailUrl não configurada.");
            }
            return Ok(authOperationResult.LoginResponse ?? new LoginResponseDto { Message = "Registro bem-sucedido. Verifique seu e-mail para confirmação." });
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
                // É importante retornar um AuthResult para consistência, mesmo que o erro seja do Turnstile
                return BadRequest(AuthResult.Failed("Falha na verificação de segurança (CAPTCHA). Por favor, tente novamente."));
            }
            _logger.LogInformation("Turnstile validado com sucesso para login. Hostname: {Hostname}", turnstileResult.Hostname);


            var result = await _authService.LoginLocalAsync(loginDto);
            if (!result.Succeeded)
            {
                if (result.IsLockedOut || result.IsNotAllowed) return Unauthorized(result);
                return BadRequest(result);
            }
            return Ok(result.LoginResponse);
        }


        [HttpGet("/api/v{version:apiVersion}/auth/external-login-challenge")]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginChallenge([FromQuery] string provider)
        {
            // Não é comum proteger o início do desafio OAuth com Turnstile,
            // pois o desafio real acontece no site do provedor externo.
            // A proteção é mais relevante no callback ou no login/registro local.
            if (string.IsNullOrWhiteSpace(provider))
            {
                return BadRequest(AuthResult.Failed("O nome do provedor externo é obrigatório."));
            }

            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Auth", values: null, protocol: Request.Scheme);
            _logger.LogInformation("Gerando desafio para provedor {Provider}. O SignInManager usará o CallbackPath configurado.", provider);

            var challenge = await _authService.ChallengeExternalLoginAsync(provider, redirectUrl!);

            if (challenge == null)
            {
                return BadRequest(AuthResult.Failed($"Provedor externo '{provider}' não suportado ou erro na configuração."));
            }
            return challenge;
        }

        [HttpGet("/api/v{version:apiVersion}/auth/external-login-callback")]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback()
        {
            // O callback do provedor externo geralmente não é protegido por Turnstile,
            // pois a interação do usuário já ocorreu no site do provedor.
            _logger.LogInformation("Recebido callback do login externo.");
            var result = await _authService.HandleExternalLoginCallbackAsync();

            var frontendRedirectUrl = _urlCallbackSettings.GetValueByKey("ExternalLoginReturnUrl");

            if (string.IsNullOrWhiteSpace(frontendRedirectUrl))
            {
                _logger.LogError("ExternalLoginCallbackUrl não configurada.");
                if (!result.Succeeded) return BadRequest(result);
                return Ok(result.LoginResponse);
            }

            if (!result.Succeeded)
            {
                _logger.LogError("Falha no HandleExternalLoginCallbackAsync: {Errors}", string.Join(", ", result.Errors));
                var errorUri = QueryHelpers.AddQueryString(frontendRedirectUrl, "error", result.Errors.FirstOrDefault() ?? "external_login_failed");
                return Redirect(errorUri);
            }

            var queryParams = new Dictionary<string, string?>
            {
                { "token", result.LoginResponse?.Token },
                { "userId", result.LoginResponse?.UserId.ToString() },
                { "username", result.LoginResponse?.Username },
                { "email", result.LoginResponse?.Email }
            };

            // Filtra parâmetros nulos e constrói a string do fragmento: "key1=value1&key2=value2"
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
            // Poderia adicionar Turnstile aqui se este endpoint for muito abusado.
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var (identityResult, user, token) = await _authService.GenerateEmailConfirmationTokenAsync(dto.Email);
            if (identityResult.Succeeded && user != null && token != null)
            {
                var frontendConfirmEmailUrl = _urlCallbackSettings.GetValueByKey("ConfirmEmailUrl");
                if (!string.IsNullOrWhiteSpace(frontendConfirmEmailUrl))
                {
                    var callbackUrl = $"{frontendConfirmEmailUrl}?userId={user.Id}&token={token}";
                    await _emailService.SendEmailConfirmationLinkAsync(user.Email, user.UserName, callbackUrl);
                }
                else _logger.LogError("ConfirmEmailUrl não configurada.");
            }
            return Ok(new { Message = "Se sua conta existir e precisar de confirmação, um e-mail foi enviado com as instruções." });
        }

        [HttpPost("/api/v{version:apiVersion}/auth/confirm-email")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailDto confirmEmailDto)
        {
            // Geralmente não protegido por Turnstile, pois o link é de uso único e enviado ao usuário.
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _authService.ConfirmEmailAsync(confirmEmailDto);
            if (!result.Succeeded) return BadRequest(new { Message = "Falha ao confirmar o e-mail.", Errors = result.Errors.Select(e => e.Description) });
            return Ok(new { Message = "E-mail confirmado com sucesso. Você já pode fazer login." });
        }

        [HttpPost("/api/v{version:apiVersion}/auth/forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
        {
            // Proteger este endpoint com Turnstile é uma boa prática.
            // Por simplicidade, não adicionei aqui, mas seria similar ao /register e /login.
            // Se for adicionar, o ForgotPasswordDto precisaria do campo TurnstileToken.
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var (identityResult, user, token) = await _authService.GeneratePasswordResetTokenAsync(forgotPasswordDto);
            if (identityResult.Succeeded && user != null && token != null)
            {
                var frontendResetPasswordUrl = _urlCallbackSettings.GetValueByKey("ResetPasswordUrl");
                if (!string.IsNullOrWhiteSpace(frontendResetPasswordUrl))
                {
                    var callbackUrl = $"{frontendResetPasswordUrl}?email={HttpUtility.UrlEncode(user.Email)}&token={token}";
                    await _emailService.SendPasswordResetLinkAsync(user.Email, user.UserName, callbackUrl);
                }
                else _logger.LogError("ResetPasswordUrl não configurada.");
            }
            return Ok(new { Message = "Se sua conta existir e for elegível, um e-mail foi enviado com instruções para redefinir sua senha." });
        }

        [HttpPost("/api/v{version:apiVersion}/auth/reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
        {
            // Geralmente não protegido por Turnstile, pois o link é de uso único.
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _authService.ResetPasswordAsync(resetPasswordDto);
            if (!result.Succeeded) return BadRequest(new { Message = "Falha ao redefinir a senha.", Errors = result.Errors.Select(e => e.Description) });
            return Ok(new { Message = "Senha redefinida com sucesso." });
        }
    }
}
