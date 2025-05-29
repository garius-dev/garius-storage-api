using Asp.Versioning;
using GariusStorage.Api.Application.Dtos.Company;
using GariusStorage.Api.Application.Interfaces;
using GariusStorage.Api.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GariusStorage.Api.WebApi.Controllers.v1
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/companies")]
    [ApiController]
    [Authorize]
    public class CompanyController : ControllerBase
    {
        private readonly ICompanyService _companyService;
        private readonly ILogger<CompanyController> _logger;

        public CompanyController(ICompanyService companyService, ILogger<CompanyController> logger)
        {
            _companyService = companyService;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Roles = $"{RoleConstants.DeveloperRoleName},{RoleConstants.OwnerRoleName}")] // ALTERADO AQUI
        [ProducesResponseType(typeof(IEnumerable<CompanyDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAllCompanies()
        {
            var companies = await _companyService.GetAllCompaniesAsync(User);
            return Ok(companies);
        }

        [HttpPost]
        [ProducesResponseType(typeof(CompanyDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateCompany([FromForm] CreateCompanyRequestDto createCompanyDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var companyDto = await _companyService.CreateCompanyAsync(createCompanyDto, User);
            return CreatedAtAction(nameof(GetCompanyById), new { id = companyDto.Id, version = "1.0" }, companyDto);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(CompanyDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateCompany(Guid id, [FromForm] UpdateCompanyRequestDto updateCompanyDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var companyDto = await _companyService.UpdateCompanyAsync(id, updateCompanyDto, User);
            return Ok(companyDto);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(CompanyDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCompanyById(Guid id)
        {
            var companyDto = await _companyService.GetCompanyByIdAsync(id, User);
            return Ok(companyDto);
        }

        [HttpPatch("{id}/inactivate")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> InactivateCompany(Guid id)
        {
            await _companyService.InactivateCompanyAsync(id, User);
            return NoContent();
        }

        [HttpPatch("{id}/activate")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ActivateCompany(Guid id)
        {
            await _companyService.ActivateCompanyAsync(id, User);
            return NoContent();
        }
    }
}
