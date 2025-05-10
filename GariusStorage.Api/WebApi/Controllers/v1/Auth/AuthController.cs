using Asp.Versioning;
using GariusStorage.Api.Application.Dtos.Auth;
using GariusStorage.Api.Application.Interfaces;
using GariusStorage.Api.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using System.Web; // Para HttpUtility.UrlEncode nos links de exemplo

namespace GariusStorage.Api.WebApi.Controllers.v1.Auth
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IEmailService _emailService; // Injetar IEmailService
        private readonly ILogger<AuthController> _logger;
        private readonly IConfiguration _configuration; // Para ler URLs do frontend
        private readonly ResendUrlCallbackSettings _resendUrlCallbackSettings; // Para o Resend

        public AuthController(
            IAuthService authService,
            IEmailService emailService, // Adicionar aqui
            ILogger<AuthController> logger,
            IConfiguration configuration,
            IOptions<ResendUrlCallbackSettings> resendUrlCallbackSettings) // Adicionar IConfiguration
        {
            _authService = authService;
            _emailService = emailService; // Atribuir
            _logger = logger;
            _configuration = configuration; // Atribuir
            _resendUrlCallbackSettings = resendUrlCallbackSettings.Value; // Atribuir
        }

        [HttpPost("register")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(AuthResult), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RegisterLocalUser([FromBody] RegisterRequestDto registerDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var authOperationResult = await _authService.RegisterLocalUserAsync(registerDto);

            if (!authOperationResult.Succeeded)
            {
                return BadRequest(authOperationResult);
            }

            // Enviar e-mail de confirmação após registro bem-sucedido
            var (identityResult, user, token) = await _authService.GenerateEmailConfirmationTokenAsync(registerDto.Email);
            if (identityResult.Succeeded && user != null && token != null)
            {
                // É crucial que esta URL aponte para o seu FRONTEND, não para a API diretamente.
                // O frontend então fará uma chamada para o endpoint /confirm-email da API.
                var frontendConfirmEmailUrl = _resendUrlCallbackSettings.ConfirmEmailUrl; // Ex: "https://meufrontend.com/confirm-email"
                if (string.IsNullOrWhiteSpace(frontendConfirmEmailUrl))
                {
                    _logger.LogError("URL de confirmação de e-mail do frontend não configurada em FrontendUrls:ConfirmEmailUrl.");
                    // Prosseguir sem enviar e-mail ou retornar um erro específico
                }
                else
                {
                    var callbackUrl = $"{frontendConfirmEmailUrl}?userId={user.Id}&token={token}"; // token já está UrlEncoded pelo AuthService
                    _logger.LogInformation("Enviando link de confirmação de e-mail para {UserEmail}. Link: {CallbackUrl}", user.Email, callbackUrl);
                    await _emailService.SendEmailConfirmationLinkAsync(user.Email, user.UserName, callbackUrl);
                }
            }
            else
            {
                _logger.LogWarning("Não foi possível gerar o token de confirmação de e-mail para {UserEmail} após o registro ou usuário não requer.", registerDto.Email);
            }

            return Ok(authOperationResult.LoginResponse ?? new LoginResponseDto { Message = "Registro bem-sucedido. Verifique seu e-mail para confirmação." });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(AuthResult), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(AuthResult), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> LoginLocal([FromBody] LoginRequestDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.LoginLocalAsync(loginDto);

            if (!result.Succeeded)
            {
                if (result.IsLockedOut || result.IsNotAllowed)
                {
                    return Unauthorized(result);
                }
                return BadRequest(result);
            }

            return Ok(result.LoginResponse);
        }

        [HttpGet("external-login-challenge")]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginChallenge([FromQuery] string provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return BadRequest(AuthResult.Failed("O nome do provedor externo é obrigatório."));
            }

            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Auth", values: null, protocol: Request.Scheme);
            _logger.LogInformation("Gerando desafio para provedor {Provider}. O SignInManager usará o CallbackPath configurado para este provedor.", provider);


            var challenge = await _authService.ChallengeExternalLoginAsync(provider, redirectUrl!); // redirectUrl aqui é para o Challenge, não necessariamente o que o Google usa para retornar.

            if (challenge == null)
            {
                return BadRequest(AuthResult.Failed($"Provedor externo '{provider}' não suportado ou erro na configuração."));
            }
            return challenge;
        }

        [HttpGet("external-login-callback")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(AuthResult), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(AuthResult), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ExternalLoginCallback()
        {
            _logger.LogInformation("Recebido callback do login externo.");
            var result = await _authService.HandleExternalLoginCallbackAsync();

            // URL do seu frontend para onde redirecionar após o login
            //var frontendRedirectUrl = _configuration["FrontendUrls:ExternalLoginCallbackUrl"]; // Ex: "https://SEU_FRONTEND_URL/auth/external-callback"
            var frontendRedirectUrl = "https://localhost:7015/home/TestesCallback"; // Ex: "https://SEU_FRONTEND_URL/auth/external-callback"

            if (string.IsNullOrWhiteSpace(frontendRedirectUrl))
            {
                _logger.LogError("FrontendUrls:ExternalLoginCallbackUrl não está configurada no appsettings.json. Não é possível redirecionar o usuário com o token.");
                // Como fallback, retorna o JSON, mas isso não é ideal para UX.
                if (!result.Succeeded) return BadRequest(result);
                return Ok(result.LoginResponse); // Fallback
            }

            if (!result.Succeeded)
            {
                _logger.LogError("Falha no HandleExternalLoginCallbackAsync: {Errors}", string.Join(", ", result.Errors));
                // Redireciona para o frontend com uma mensagem de erro
                var errorUri = QueryHelpers.AddQueryString(frontendRedirectUrl, "error", result.Errors.FirstOrDefault() ?? "external_login_failed");
                return Redirect(errorUri);
            }

            // Redireciona para o frontend com o token JWT e outras informações no fragmento hash (mais seguro)
            // ou como query string se preferir.
            var queryParams = new Dictionary<string, string?>
            {
                { "token", result.LoginResponse?.Token },
                { "userId", result.LoginResponse?.UserId.ToString() },
                { "username", result.LoginResponse?.Username },
                { "email", result.LoginResponse?.Email }
                // Adicione outras informações se necessário
            };

            // Usando fragmento hash (#) é uma boa prática para tokens, pois não são enviados ao servidor
            var successUri = frontendRedirectUrl + "#" + string.Join("&", queryParams.Where(kvp => kvp.Value != null).Select(kvp => $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}"));
            // Ou usando query string:
            //var successUri = QueryHelpers.AddQueryString(frontendRedirectUrl, queryParams!);

            _logger.LogInformation("Redirecionando para o frontend após login externo bem-sucedido: {SuccessUri}", successUri);
            return Redirect(successUri);
        }

        [HttpPost("confirm-email-request")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RequestEmailConfirmation([FromBody] ForgotPasswordDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var (identityResult, user, token) = await _authService.GenerateEmailConfirmationTokenAsync(dto.Email);

            if (!identityResult.Succeeded || user == null || token == null)
            {
                _logger.LogWarning("Falha ao gerar token de confirmação de e-mail para {Email} ou e-mail já confirmado/não requer.", dto.Email);
            }
            else
            {
                var frontendConfirmEmailUrl = _resendUrlCallbackSettings.ConfirmEmailUrl;
                if (string.IsNullOrWhiteSpace(frontendConfirmEmailUrl))
                {
                    _logger.LogError("URL de confirmação de e-mail do frontend não configurada em FrontendUrls:ConfirmEmailUrl.");
                }
                else
                {
                    var callbackUrl = $"{frontendConfirmEmailUrl}?userId={user.Id}&token={token}";
                    _logger.LogInformation("Enviando link de confirmação de e-mail para {UserEmail}. Link: {CallbackUrl}", user.Email, callbackUrl);
                    await _emailService.SendEmailConfirmationLinkAsync(user.Email, user.UserName, callbackUrl);
                }
            }
            // Sempre retorne uma mensagem genérica para não vazar informações
            return Ok(new { Message = "Se sua conta existir e precisar de confirmação, um e-mail foi enviado com as instruções." });
        }


        [HttpPost("confirm-email")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(SerializableError), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailDto confirmEmailDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var result = await _authService.ConfirmEmailAsync(confirmEmailDto);

            if (!result.Succeeded)
            {
                return BadRequest(new { Message = "Falha ao confirmar o e-mail.", Errors = result.Errors.Select(e => e.Description) });
            }

            return Ok(new { Message = "E-mail confirmado com sucesso. Você já pode fazer login." });
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (identityResult, user, token) = await _authService.GeneratePasswordResetTokenAsync(forgotPasswordDto);

            if (identityResult.Succeeded && user != null && token != null)
            {
                var frontendResetPasswordUrl = _resendUrlCallbackSettings.ResetPasswordUrl; // Ex: "https://meufrontend.com/reset-password"
                if (string.IsNullOrWhiteSpace(frontendResetPasswordUrl))
                {
                    _logger.LogError("URL de reset de senha do frontend não configurada em FrontendUrls:ResetPasswordUrl.");
                }
                else
                {
                    // O token já vem UrlEncoded do AuthService
                    var callbackUrl = $"{frontendResetPasswordUrl}?email={HttpUtility.UrlEncode(user.Email)}&token={token}";
                    _logger.LogInformation("Enviando link de reset de senha para {UserEmail}. Link: {CallbackUrl}", user.Email, callbackUrl);
                    await _emailService.SendPasswordResetLinkAsync(user.Email, user.UserName, callbackUrl);
                }
            }
            else
            {
                _logger.LogWarning("Geração de token de reset de senha não produziu um token para {Email}, ou o usuário não é elegível.", forgotPasswordDto.Email);
            }

            return Ok(new { Message = "Se sua conta existir e for elegível, um e-mail foi enviado com instruções para redefinir sua senha." });
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(SerializableError), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.ResetPasswordAsync(resetPasswordDto);

            if (!result.Succeeded)
            {
                return BadRequest(new { Message = "Falha ao redefinir a senha.", Errors = result.Errors.Select(e => e.Description) });
            }

            return Ok(new { Message = "Senha redefinida com sucesso." });
        }
    }
}
