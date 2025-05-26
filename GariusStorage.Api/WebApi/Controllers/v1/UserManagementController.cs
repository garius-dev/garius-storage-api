using Asp.Versioning;
using GariusStorage.Api.Application.Dtos;
using GariusStorage.Api.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GariusStorage.Api.WebApi.Controllers.v1
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/users")]
    [ApiController]
    public class UserManagementController : ControllerBase
    {
        private readonly IUserManagementService _userManagementService;
        private readonly ILogger<UserManagementController> _logger;

        public UserManagementController(
            IUserManagementService userManagementService,
            ILogger<UserManagementController> logger)
        {
            _userManagementService = userManagementService;
            _logger = logger;
        }

        [Authorize]
        [HttpGet("profile/{emailOrUsername}")]
        [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)] // Se a policy base não for suficiente
        public async Task<IActionResult> GetUserProfile(string emailOrUsername)
        {
            // A policy [Authorize] no controller já faz uma verificação.
            // A lógica de quem pode ver quem pode ser adicionada no serviço se necessário,
            // mas para GET, geralmente se o requisitante tem a role para acessar o endpoint, ele pode ver.

            var userProfile = await _userManagementService.GetUserProfileAsync(emailOrUsername);
            if (userProfile == null)
            {
                return NotFound(new { Message = $"Usuário '{emailOrUsername}' não encontrado." });
            }
            return Ok(userProfile);
        }

        [Authorize]
        [HttpPut("profile/{emailOrUsername}")]
        [ProducesResponseType(typeof(UpdateUserProfileResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(UpdateUserProfileResultDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(UpdateUserProfileResultDto), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)] // Se o usuário alvo não for encontrado
        // Uma policy mais específica poderia ser usada aqui se a do controller não for suficiente
        // [Authorize(Policy = AuthorizationPolicies.CanEditUserProfilesPolicy)] // Exemplo
        public async Task<IActionResult> UpdateUserProfile(string emailOrUsername, [FromBody] UserProfileDto profileUpdateDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // O ClaimsPrincipal do usuário que está fazendo a requisição é passado para o serviço
            // para que a lógica de autorização hierárquica possa ser aplicada.
            var result = await _userManagementService.UpdateUserProfileAsync(emailOrUsername, profileUpdateDto, User);

            if (!result.Succeeded)
            {
                if (result.Message.Contains("não encontrado")) // Simplificação para distinguir NotFound de outros erros
                {
                    return NotFound(result);
                }
                if (result.Message.Contains("Não autorizado"))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, result);
                }
                return BadRequest(result);
            }

            return Ok(result);
        }
    }
}
