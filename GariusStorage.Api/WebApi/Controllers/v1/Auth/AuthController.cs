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

namespace GariusStorage.Api.WebApi.Controllers.v1.Auth
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ITokenService _tokenService;
        private readonly AuthenticationSettings _externalAuthSettings;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ITokenService tokenService,
            IOptions<AuthenticationSettings> externalAuthSettings,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _externalAuthSettings = externalAuthSettings.Value;
            _logger = logger;
        }

    }
}
