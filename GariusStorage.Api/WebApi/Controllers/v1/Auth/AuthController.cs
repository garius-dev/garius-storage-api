using Asp.Versioning;
using GariusStorage.Api.Application.Dtos;
using GariusStorage.Api.Domain.Entities.Identity;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Google.Apis.Auth;
using GariusStorage.Api.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.Cookies;
using GariusStorage.Api.Application.Interfaces;
using GariusStorage.Api.Application.Dtos.Auth;

namespace GariusStorage.Api.WebApi.Controllers.v1.Auth
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;
        // Se for construir URLs de callback/confirmação no controller:
        // private readonly LinkGenerator _linkGenerator;

        public AuthController(
            IAuthService authService,
            SignInManager<GariusStorage.Api.Domain.Entities.Identity.ApplicationUser> signInManager,
            ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
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

            var result = await _authService.RegisterLocalUserAsync(registerDto);

            if (!result.Succeeded)
            {
                return BadRequest(result); // Retorna o AuthResult com os erros
            }

            // Opcional: Se quiser enviar o email de confirmação a partir daqui
            // var (identityResult, user, token) = await _authService.GenerateEmailConfirmationTokenAsync(registerDto.Email);
            // if (identityResult.Succeeded && user != null && token != null)
            // {
            //     var callbackUrl = Url.Action(nameof(ConfirmEmail), "Auth", new { userId = user.Id, token = token }, Request.Scheme);
            //     // await _emailService.SendEmailAsync(user.Email, "Confirme seu e-mail", $"Por favor, confirme sua conta clicando aqui: <a href='{callbackUrl}'>link</a>");
            //      _logger.LogInformation("Link de confirmação gerado (precisa ser enviado): {CallbackUrl}", callbackUrl);
            // }


            // Para registro, geralmente não se retorna o token diretamente, mas uma mensagem de sucesso.
            // O LoginResponseDto no AuthResult pode ser adaptado para isso.
            return Ok(result.LoginResponse ?? new LoginResponseDto { Message = "Registro bem-sucedido. Verifique seu e-mail para confirmação." });
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
                    return Unauthorized(result); // 401 com detalhes
                }
                return BadRequest(result); // 400 com detalhes
            }

            return Ok(result.LoginResponse);
        }

        [HttpGet("external-login")] // Ex: /api/v1/auth/external-login?provider=Google
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLogin([FromQuery] string provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return BadRequest(AuthResult.Failed("O nome do provedor externo é obrigatório."));
            }

            // O URL de callback deve ser o endpoint que manipula o resultado do login externo.
            // Certifique-se que este endpoint está configurado corretamente no seu provedor (Google Console, etc.)
            // e no Program.cs (options.CallbackPath)
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Auth", values: null, protocol: Request.Scheme);
            if (string.IsNullOrEmpty(redirectUrl))
            {
                _logger.LogError("Não foi possível gerar a URL de redirecionamento para o login externo.");
                return StatusCode(StatusCodes.Status500InternalServerError, AuthResult.Failed("Erro ao iniciar login externo."));
            }

            var challenge = await _authService.ChallengeExternalLoginAsync(provider, redirectUrl);

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
            var result = await _authService.HandleExternalLoginCallbackAsync();

            if (!result.Succeeded)
            {
                // Se o frontend espera um redirecionamento com o token/erro na query string:
                // var queryParams = new Dictionary<string, string>();
                // if (result.LoginResponse?.Token != null) queryParams["token"] = result.LoginResponse.Token;
                // else queryParams["error"] = result.Errors.FirstOrDefault() ?? "external_login_failed";
                // return Redirect(QueryHelpers.AddQueryString("https://seu-frontend.com/login-callback", queryParams));

                // Se o frontend faz a chamada e espera um JSON:
                if (result.IsLockedOut || result.IsNotAllowed)
                {
                    return Unauthorized(result);
                }
                return BadRequest(result);
            }

            // Similarmente, pode redirecionar ou retornar JSON
            return Ok(result.LoginResponse);
        }

        [HttpPost("confirm-email-request")]
        [AllowAnonymous] // Ou [Authorize] se o usuário já estiver logado mas não confirmado
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RequestEmailConfirmation([FromBody] ForgotPasswordDto dto) // Reutilizando DTO
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var (identityResult, user, token) = await _authService.GenerateEmailConfirmationTokenAsync(dto.Email);

            if (!identityResult.Succeeded || user == null || token == null)
            {
                // Não revele se o email existe ou não, ou se já está confirmado.
                _logger.LogWarning("Falha ao gerar token de confirmação de e-mail para {Email} ou e-mail já confirmado/não requer.", dto.Email);
                return Ok(new { Message = "Se sua conta existir e precisar de confirmação, um e-mail foi enviado." });
            }

            // Construir o link de confirmação. O frontend geralmente lida com isso.
            // Exemplo: var confirmationLink = $"https://seu-frontend.com/confirm-email?userId={user.Id}&token={token}";
            // await _emailService.SendEmailAsync(user.Email, "Confirme seu E-mail", $"Clique aqui para confirmar: <a href='{confirmationLink}'>Confirmar</a>");
            _logger.LogInformation("Token de confirmação de e-mail gerado para {Email}. O frontend deve construir o link e o backend (via EmailService) deve enviá-lo. Token: {Token}", dto.Email, token);

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

            // Mesmo que identityResult seja sucesso, user e token podem ser null se o usuário não for elegível (externo, não confirmado, não existe)
            // Isso é para evitar vazamento de informação.
            if (identityResult.Succeeded && user != null && token != null)
            {
                // O frontend geralmente constrói este link
                // Exemplo: var resetLink = $"https://seu-frontend.com/reset-password?email={HttpUtility.UrlEncode(user.Email)}&token={token}";
                // await _emailService.SendPasswordResetLinkAsync(user, resetLink);
                _logger.LogInformation("Token de reset de senha gerado para {Email}. O frontend deve construir o link e o backend (via EmailService) deve enviá-lo. Token: {Token}", user.Email, token);
            }
            else
            {
                _logger.LogWarning("Geração de token de reset de senha não produziu um token para {Email}, ou o usuário não é elegível.", forgotPasswordDto.Email);
            }

            // Sempre retorne uma mensagem genérica para não vazar se o email existe ou não.
            return Ok(new { Message = "Se sua conta existir, um e-mail foi enviado com instruções para redefinir sua senha." });
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
