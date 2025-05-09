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

namespace GariusStorage.Api.WebApi.Controllers.v1.Auth
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly AuthenticationSettings _externalAuthSettings;
        private readonly IConfiguration _configuration;

        public AuthController(SignInManager<ApplicationUser> signInManager,
            IOptions<AuthenticationSettings> externalAuthSettings,
            IConfiguration configuration)
        {
            _signInManager = signInManager;
            _externalAuthSettings = externalAuthSettings.Value;
            _configuration = configuration;
        }

        [HttpGet("login/google")]
        public IActionResult LoginWithGoogle(string? returnUrl = null)
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = Url.Action(nameof(GoogleCallback))
            };

            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet("google-callback")]
        public async Task<IActionResult> GoogleCallback()
        {
            return Ok();
        }
    }
}
