using Asp.Versioning;
using GariusStorage.Api.Application.Dtos.Auth;
using GariusStorage.Api.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GariusStorage.Api.Configuration;
using Microsoft.Extensions.Options;
using GariusStorage.Api.Extensions;
using GariusStorage.Api.Application.Exceptions;
using System.Threading.Tasks;
using System.Linq;
using System.Web;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace GariusStorage.Api.WebApi.Controllers.v1.Auth
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/auth")]
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

            // A filtragem de UrlCallbacks é feita uma vez no construtor.
            if (!string.IsNullOrEmpty(_urlCallbackSettings.Environment) && _urlCallbackSettings.UrlCallbacks != null && _urlCallbackSettings.UrlCallbacks.Any())
            {
                // Esta linha modifica o dicionário original. Se precisar do original em outro lugar, clone antes de filtrar.
                _urlCallbackSettings.UrlCallbacks.FilterKeysStartingWith(_urlCallbackSettings.Environment);
            }
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterLocalUser([FromBody] RegisterRequestDto registerDto)
        {
            if (!ModelState.IsValid)
            {
                // O atributo [ApiController] já transforma ModelState inválido em BadRequest(ProblemDetails)
                return BadRequest(ModelState);
            }

            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            // ValidateTokenAsync agora lança exceção em caso de falha.
            var turnstileResult = await _turnstileService.ValidateTokenAsync(registerDto.TurnstileToken, remoteIp);
            _logger.LogInformation("Turnstile validado com sucesso para registro. Hostname: {Hostname}", turnstileResult.Hostname);

            // AuthService.RegisterLocalUserAsync agora retorna Task (void) ou lança exceção
            await _authService.RegisterLocalUserAsync(registerDto);

            // Lógica de envio de e-mail e fallback
            try
            {
                // GenerateEmailConfirmationTokenAsync lança exceção se o usuário não for elegível (ex: já confirmado, externo)
                var (user, token) = await _authService.GenerateEmailConfirmationTokenAsync(registerDto.Email);

                var frontendConfirmEmailUrl = _urlCallbackSettings.GetValueByKey("ConfirmEmailUrl");
                if (string.IsNullOrWhiteSpace(frontendConfirmEmailUrl) || frontendConfirmEmailUrl == "/")
                {
                    _logger.LogError("ConfirmEmailUrl não configurada ou inválida. O e-mail de confirmação não será enviado para {UserEmail}.", user.Email);
                    // O registro foi bem-sucedido, mas o e-mail não pode ser enviado.
                    // A mensagem de sucesso genérica ainda será retornada.
                }
                else
                {
                    var callbackUrl = $"{frontendConfirmEmailUrl}?userId={user.Id}&token={token}";
                    // SendEmailConfirmationLinkAsync agora lança exceção em caso de falha no envio.
                    await _emailService.SendEmailConfirmationLinkAsync(user.Email, user.UserName, callbackUrl);
                }
            }
            catch (OperationFailedException ex) when (ex.ErrorCode == "EMAIL_SERVICE_UNAVAILABLE" || ex.ErrorCode == "EMAIL_SEND_FAILED_API_ERROR" || ex.ErrorCode == "EMAIL_PAYLOAD_SERIALIZATION_ERROR")
            {
                // Captura específica para falhas no envio de e-mail pelo EmailService
                _logger.LogError(ex, "Falha crítica ao enviar o e-mail de confirmação para {Email} após o registro. Iniciando fallback para deletar usuário.", registerDto.Email);
                try
                {
                    await _authService.FallbackRegisterAndDeleteUserAsync(registerDto.Email);
                    // Se o fallback for bem-sucedido, lance uma exceção para informar o usuário sobre o problema do e-mail e o rollback.
                    throw new OperationFailedException("Seu registro foi processado, mas houve uma falha ao enviar o e-mail de confirmação. O registro foi desfeito. Por favor, tente se registrar novamente.", "EMAIL_CONFIRMATION_SEND_FAILED_ROLLEDBACK", null, ex);
                }
                catch (Exception fallbackEx)
                {
                    // Se o fallback também falhar (ex: NotFoundException, OperationFailedException),
                    // isso é um problema sério. Deixe o ErrorHandlingMiddleware pegar essa exceção crítica do fallback.
                    _logger.LogCritical(fallbackEx, "Falha crítica no fallback ao tentar deletar o usuário {Email} após falha no envio de e-mail.", registerDto.Email);
                    throw; // Re-lança a exceção do fallback para ser tratada pelo middleware
                }
            }
            catch (NotFoundException ex)
            {
                // Esta exceção pode vir de GenerateEmailConfirmationTokenAsync se o usuário recém-criado não for encontrado (improvável mas possível)
                // ou de FallbackRegisterAndDeleteUserAsync.
                _logger.LogError(ex, "Erro inesperado durante o processo de finalização do registro para {Email}.", registerDto.Email);
                // Deixe o ErrorHandlingMiddleware tratar.
                throw;
            }
            catch (ValidationException ex)
            {
                // Esta exceção pode vir de GenerateEmailConfirmationTokenAsync (ex: email já confirmado, usuário externo)
                _logger.LogWarning(ex, "Problema de validação ao tentar gerar token de confirmação para {Email} durante o registro.", registerDto.Email);
                // Deixe o ErrorHandlingMiddleware tratar.
                throw;
            }
            // Outras exceções de _authService.RegisterLocalUserAsync ou _authService.GenerateEmailConfirmationTokenAsync
            // serão pegas pelo ErrorHandlingMiddleware.

            return Ok(new { Message = "Registro bem-sucedido. Por favor, verifique seu e-mail para confirmar sua conta." });
        }


        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> LoginLocal([FromBody] LoginRequestDto loginDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            // ValidateTokenAsync lança exceção em caso de falha.
            var turnstileResult = await _turnstileService.ValidateTokenAsync(loginDto.TurnstileToken, remoteIp);
            _logger.LogInformation("Turnstile validado com sucesso para login. Hostname: {Hostname}", turnstileResult.Hostname);

            // AuthService.LoginLocalAsync agora retorna LoginResponseDto ou lança exceção
            var loginResponse = await _authService.LoginLocalAsync(loginDto);
            return Ok(loginResponse);
        }

        [HttpGet("external-login-challenge")]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginChallenge([FromQuery] string provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                throw new ValidationException("O nome do provedor externo é obrigatório.", "PROVIDER_NAME_REQUIRED");
            }

            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Auth", values: null, protocol: Request.Scheme);
            _logger.LogInformation("Gerando desafio para provedor {Provider}. O SignInManager usará o CallbackPath configurado.", provider);

            // ChallengeExternalLoginAsync retorna ChallengeResult diretamente ou lança exceção se o provider não for encontrado pelo SignInManager.
            var challenge = await _authService.ChallengeExternalLoginAsync(provider, redirectUrl!);

            if (challenge == null) // Embora o método no serviço retorne Task<ChallengeResult>, pode ser null se algo der errado internamente antes do Challenge
            {
                throw new OperationFailedException($"Provedor externo '{provider}' não suportado ou erro na configuração.", "EXTERNAL_PROVIDER_CHALLENGE_ERROR");
            }

            return challenge;
        }

        [HttpGet("external-login-callback")]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback()
        {
            _logger.LogInformation("Recebido callback do login externo.");
            // HandleExternalLoginCallbackAsync retorna LoginResponseDto ou lança exceção
            var loginResponse = await _authService.HandleExternalLoginCallbackAsync();

            var frontendRedirectUrl = _urlCallbackSettings.GetValueByKey("ExternalLoginReturnUrl");

            if (string.IsNullOrWhiteSpace(frontendRedirectUrl) || frontendRedirectUrl == "/")
            {
                _logger.LogWarning("ExternalLoginReturnUrl não configurada ou inválida. Retornando token diretamente no corpo da resposta.");
                return Ok(loginResponse);
            }

            var queryParams = new Dictionary<string, string?>
            {
                { "token", loginResponse.Token },
                { "userId", loginResponse.UserId.ToString() },
                { "username", loginResponse.Username },
                { "email", loginResponse.Email }
                // Adicionar roles aqui se o frontend precisar delas imediatamente no fragmento
                // { "roles", string.Join(",", loginResponse.Roles ?? Enumerable.Empty<string>()) } 
            };

            var fragmentString = string.Join("&", queryParams
                .Where(kvp => kvp.Value != null)
                .Select(kvp => $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}"));

            var successUri = $"{frontendRedirectUrl}#{fragmentString}";

            _logger.LogInformation("Redirecionando para o frontend após login externo bem-sucedido: {SuccessUri}", successUri);
            return Redirect(successUri);
        }


        [HttpPost("confirm-email-request")]
        [AllowAnonymous]
        public async Task<IActionResult> RequestEmailConfirmation([FromBody] ForgotPasswordDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                // GenerateEmailConfirmationTokenAsync lança exceção se usuário não encontrado, externo ou já confirmado.
                var (user, token) = await _authService.GenerateEmailConfirmationTokenAsync(dto.Email);

                var frontendConfirmEmailUrl = _urlCallbackSettings.GetValueByKey("ConfirmEmailUrl");
                if (!string.IsNullOrWhiteSpace(frontendConfirmEmailUrl) && frontendConfirmEmailUrl != "/")
                {
                    var callbackUrl = $"{frontendConfirmEmailUrl}?userId={user.Id}&token={token}";
                    // SendEmailConfirmationLinkAsync agora lança exceção em caso de falha no envio.
                    await _emailService.SendEmailConfirmationLinkAsync(user.Email, user.UserName, callbackUrl);
                }
                else
                {
                    _logger.LogError("ConfirmEmailUrl não configurada ou inválida. O e-mail de confirmação não será enviado para {Email}", dto.Email);
                }
            }
            catch (NotFoundException ex)
            {
                _logger.LogWarning(ex, "Solicitação de confirmação de e-mail para usuário não encontrado ou não elegível: {Email}", dto.Email);
                // Não vaze a informação se o usuário existe ou não. Retorne a mesma mensagem de sucesso genérica.
            }
            catch (ValidationException ex)
            {
                // Captura casos como EMAIL_ALREADY_CONFIRMED ou EXTERNAL_USER_NO_CONFIRMATION_NEEDED
                _logger.LogInformation(ex, "Solicitação de confirmação de e-mail para {Email}, mas não aplicável: {Message}", dto.Email, ex.Message);
                // Retorne a mesma mensagem de sucesso genérica.
            }
            catch (OperationFailedException ex) // Captura falhas no envio de e-mail pelo EmailService
            {
                _logger.LogError(ex, "Falha ao enviar e-mail de confirmação para {Email}: {Message}", dto.Email, ex.Message);
                // Ainda retorna a mensagem genérica para o usuário.
            }
            // Outras exceções inesperadas serão pegas pelo ErrorHandlingMiddleware.

            return Ok(new { Message = "Se uma conta correspondente ao seu e-mail existir e precisar de confirmação, um link foi enviado." });
        }

        [HttpPost("confirm-email")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailDto confirmEmailDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // AuthService.ConfirmEmailAsync agora retorna Task (void) ou lança exceção
            await _authService.ConfirmEmailAsync(confirmEmailDto);
            return Ok(new { Message = "E-mail confirmado com sucesso. Você já pode fazer login." });
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                // GeneratePasswordResetTokenAsync lança exceção se usuário não encontrado, externo ou email não confirmado.
                var (user, token) = await _authService.GeneratePasswordResetTokenAsync(forgotPasswordDto);

                var frontendResetPasswordUrl = _urlCallbackSettings.GetValueByKey("ResetPasswordUrl");
                if (!string.IsNullOrWhiteSpace(frontendResetPasswordUrl) && frontendResetPasswordUrl != "/")
                {
                    var callbackUrl = $"{frontendResetPasswordUrl}?email={HttpUtility.UrlEncode(user.Email)}&token={token}";
                    // SendPasswordResetLinkAsync agora lança exceção em caso de falha no envio.
                    await _emailService.SendPasswordResetLinkAsync(user.Email, user.UserName, callbackUrl);
                }
                else
                {
                    _logger.LogError("ResetPasswordUrl não configurada ou inválida. O e-mail de redefinição de senha não será enviado para {Email}", forgotPasswordDto.Email);
                }
            }
            catch (NotFoundException ex)
            {
                _logger.LogWarning(ex, "Solicitação de redefinição de senha para usuário não encontrado ou não elegível: {Email}", forgotPasswordDto.Email);
            }
            catch (ValidationException ex)
            {
                // Captura casos como EXTERNAL_USER_NO_PASSWORD_RESET ou EMAIL_NOT_CONFIRMED_FOR_RESET
                _logger.LogInformation(ex, "Solicitação de redefinição de senha para {Email}, mas não aplicável: {Message}", forgotPasswordDto.Email, ex.Message);
            }
            catch (OperationFailedException ex) // Captura falhas no envio de e-mail pelo EmailService
            {
                _logger.LogError(ex, "Falha ao enviar e-mail de redefinição de senha para {Email}: {Message}", forgotPasswordDto.Email, ex.Message);
            }
            // Outras exceções inesperadas serão pegas pelo ErrorHandlingMiddleware.

            return Ok(new { Message = "Se sua conta existir e for elegível, um e-mail foi enviado com instruções para redefinir sua senha." });
        }

        [HttpPost("reset-password")]
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
