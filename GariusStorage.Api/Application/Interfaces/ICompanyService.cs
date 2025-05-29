using GariusStorage.Api.Application.Dtos.Company;
using System.Security.Claims;

namespace GariusStorage.Api.Application.Interfaces
{
    public interface ICompanyService
    {
        Task<CompanyDto> CreateCompanyAsync(CreateCompanyRequestDto dto, ClaimsPrincipal performingUser);
        Task<CompanyDto> UpdateCompanyAsync(Guid companyId, UpdateCompanyRequestDto dto, ClaimsPrincipal performingUser);
        Task<CompanyDto> GetCompanyByIdAsync(Guid companyId, ClaimsPrincipal performingUser);
        Task InactivateCompanyAsync(Guid companyId, ClaimsPrincipal performingUser);
        Task ActivateCompanyAsync(Guid companyId, ClaimsPrincipal performingUser);
        Task<IEnumerable<CompanyDto>> GetAllCompaniesAsync(ClaimsPrincipal performingUser);
    }
}
