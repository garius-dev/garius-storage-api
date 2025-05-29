using AutoMapper;
using GariusStorage.Api.Application.Dtos.Company;
using GariusStorage.Api.Application.Exceptions;
using GariusStorage.Api.Application.Interfaces;
using GariusStorage.Api.Domain.Constants;
using GariusStorage.Api.Domain.Entities;
using GariusStorage.Api.Domain.Entities.Identity;
using GariusStorage.Api.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace GariusStorage.Api.Application.Services
{
    public class CompanyService : ICompanyService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly ILogger<CompanyService> _logger;

        public CompanyService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            UserManager<ApplicationUser> userManager,
            ICloudinaryService cloudinaryService,
            ILogger<CompanyService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _userManager = userManager;
            _cloudinaryService = cloudinaryService;
            _logger = logger;
        }

        public async Task<CompanyDto> CreateCompanyAsync(CreateCompanyRequestDto dto, ClaimsPrincipal performingUserPrincipal)
        {
            var performingUserId = performingUserPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(performingUserId))
            {
                _logger.LogError("Não foi possível obter o ID do usuário que está criando a empresa.");
                throw new PermissionDeniedException("Não autorizado: Não foi possível identificar o usuário requisitante.", "USER_ID_NOT_FOUND");
            }

            var performingUser = await _userManager.FindByIdAsync(performingUserId);
            if (performingUser == null)
            {
                _logger.LogError("Usuário requisitante com ID {PerformingUserId} não encontrado.", performingUserId);
                throw new PermissionDeniedException("Não autorizado: Usuário requisitante inválido.", "PERFORMING_USER_NOT_FOUND");
            }

            bool isSystemAdminOrDev = await _userManager.IsInRoleAsync(performingUser, RoleConstants.DeveloperRoleName) ||
                                      await _userManager.IsInRoleAsync(performingUser, RoleConstants.OwnerRoleName);

            if (performingUser.CompanyId.HasValue && performingUser.CompanyId != Guid.Empty && !isSystemAdminOrDev)
            {
                _logger.LogWarning("Usuário {UserId} já está associado à empresa {CompanyId} e tentou criar outra, não sendo admin/dev do sistema.", performingUserId, performingUser.CompanyId);
                throw new ConflictException("Você já está associado a uma empresa. Não é possível criar uma nova.", "USER_ALREADY_HAS_COMPANY");
            }

            var existingCompany = await _unitOfWork.Companies.ExistsAsync(c => c.CNPJ == dto.CNPJ);
            if (existingCompany)
            {
                _logger.LogWarning("Tentativa de criar empresa com CNPJ já existente: {CNPJ}", dto.CNPJ);
                throw new ConflictException($"Já existe uma empresa cadastrada com o CNPJ {dto.CNPJ}.", "CNPJ_ALREADY_EXISTS");
            }

            var company = _mapper.Map<Company>(dto);
            company.Id = Guid.NewGuid();
            company.Enabled = true;
            company.CreatedAt = DateTime.UtcNow;
            company.LastUpdate = DateTime.UtcNow;

            if (dto.ImageFile != null && dto.ImageFile.Length > 0)
            {
                using var memoryStream = new MemoryStream();
                await dto.ImageFile.CopyToAsync(memoryStream);
                var imageBytes = memoryStream.ToArray();
                try
                {
                    company.ImageUrl = await _cloudinaryService.UploadImageAsync(imageBytes, company.Id.ToString(), "company_logo", "company_logos");
                    _logger.LogInformation("Logo da empresa {CompanyId} carregada para Cloudinary.", company.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Falha ao fazer upload da imagem da empresa {CompanyId} para Cloudinary.", company.Id);
                    throw new OperationFailedException("Falha ao processar a imagem da empresa.", "IMAGE_UPLOAD_FAILED", null, ex);
                }
            }

            await _unitOfWork.Companies.AddAsync(company);

            // O usuário que cria a empresa se torna o "dono" dela (IsCompanyOwner = true)
            // e é associado a ela, e recebe a role de Admin (da empresa).
            performingUser.CompanyId = company.Id;
            performingUser.IsCompanyOwner = true;
            var userUpdateResult = await _userManager.UpdateAsync(performingUser);

            if (!userUpdateResult.Succeeded)
            {
                _logger.LogError("Falha ao atualizar CompanyId e IsCompanyOwner para o usuário {UserId} após criar a empresa {CompanyId}. Erros: {Errors}",
                   performingUserId, company.Id, string.Join(", ", userUpdateResult.Errors.Select(e => e.Description)));
                await _unitOfWork.Companies.Remove(company);
                throw new OperationFailedException("Falha ao associar o usuário à nova empresa.", "USER_COMPANY_ASSOCIATION_FAILED", userUpdateResult.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
            }
            _logger.LogInformation("Usuário {UserId} associado à nova empresa {CompanyId} e definido como IsCompanyOwner.", performingUserId, company.Id);

            // Adicionar o criador à role 'Admin' (administrador da empresa), se ele ainda não for um admin de sistema
            if (!await _userManager.IsInRoleAsync(performingUser, RoleConstants.AdminRoleName) && !isSystemAdminOrDev)
            {
                var addToAdminRoleResult = await _userManager.AddToRoleAsync(performingUser, RoleConstants.AdminRoleName);
                if (!addToAdminRoleResult.Succeeded)
                {
                    _logger.LogError("Falha ao adicionar a role '{AdminRoleName}' (Admin da empresa) ao usuário {UserId} para a empresa {CompanyId}. Erros: {Errors}",
                        RoleConstants.AdminRoleName, performingUserId, company.Id, string.Join(", ", addToAdminRoleResult.Errors.Select(e => e.Description)));
                }
                else
                {
                    _logger.LogInformation("Role '{AdminRoleName}' (Admin da empresa) adicionada ao usuário {UserId} para a empresa {CompanyId}.", RoleConstants.AdminRoleName, performingUserId, company.Id);
                }
            }
            // Não adiciona mais a role 'OwnerRoleName' (que é de sistema) aqui.

            await _unitOfWork.CommitAsync();
            _logger.LogInformation("Empresa {CompanyId} ({LegalName}) criada com sucesso pelo usuário {UserId}.", company.Id, company.LegalName, performingUserId);

            return _mapper.Map<CompanyDto>(company);
        }

        private async Task<bool> CanManageCompanyAsync(ApplicationUser performingUser, Guid companyIdToManage)
        {
            if (performingUser == null) return false;

            bool isCompanyOwnerFlag = performingUser.CompanyId == companyIdToManage && performingUser.IsCompanyOwner;
            if (isCompanyOwnerFlag) return true;

            // Verifica se é Admin da empresa específica (além de ser o IsCompanyOwner)
            // A flag IsCompanyOwner já cobre o "dono". Um Admin não-owner não gerenciaria por aqui.
            // A lógica atual permite que o IsCompanyOwner gerencie.
            // Se um "Admin" da empresa (que não é IsCompanyOwner) também pudesse gerenciar, a lógica mudaria.
            // Por ora, IsCompanyOwner é suficiente para o admin da própria empresa.

            bool isSystemDeveloper = await _userManager.IsInRoleAsync(performingUser, RoleConstants.DeveloperRoleName);
            if (isSystemDeveloper) return true;

            bool isSystemOwner = await _userManager.IsInRoleAsync(performingUser, RoleConstants.OwnerRoleName);
            if (isSystemOwner) return true;

            return false;
        }

        public async Task<CompanyDto> UpdateCompanyAsync(Guid companyId, UpdateCompanyRequestDto dto, ClaimsPrincipal performingUserPrincipal)
        {
            var company = await _unitOfWork.Companies.GetByIdAsync(companyId);
            if (company == null)
            {
                _logger.LogWarning("Empresa com ID {CompanyId} não encontrada para atualização.", companyId);
                throw new NotFoundException("Empresa não encontrada.", "COMPANY_NOT_FOUND");
            }

            var performingUserId = performingUserPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
            var performingUser = await _userManager.FindByIdAsync(performingUserId ?? "");

            if (!await CanManageCompanyAsync(performingUser, companyId))
            {
                _logger.LogWarning("Usuário {UserId} não autorizado a atualizar a empresa {CompanyId}.", performingUserId, companyId);
                throw new PermissionDeniedException("Você não tem permissão para atualizar esta empresa.", "COMPANY_UPDATE_FORBIDDEN");
            }

            _mapper.Map(dto, company);
            company.LastUpdate = DateTime.UtcNow;

            if (dto.ImageFile != null && dto.ImageFile.Length > 0)
            {
                using var memoryStream = new MemoryStream();
                await dto.ImageFile.CopyToAsync(memoryStream);
                var imageBytes = memoryStream.ToArray();
                try
                {
                    company.ImageUrl = await _cloudinaryService.UploadImageAsync(imageBytes, company.Id.ToString(), "company_logo", "company_logos");
                    _logger.LogInformation("Logo da empresa {CompanyId} atualizada no Cloudinary.", company.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Falha ao fazer upload da nova imagem da empresa {CompanyId} para Cloudinary.", company.Id);
                    throw new OperationFailedException("Falha ao processar a nova imagem da empresa.", "IMAGE_UPDATE_UPLOAD_FAILED", null, ex);
                }
            }

            if (dto.Enabled.HasValue)
            {
                // Apenas donos do sistema podem alterar 'Enabled' diretamente via PUT.
                // O dono da empresa (IsCompanyOwner) deve usar os endpoints /activate e /inactivate.
                bool isSystemDeveloper = await _userManager.IsInRoleAsync(performingUser, RoleConstants.DeveloperRoleName);
                bool isSystemOwner = await _userManager.IsInRoleAsync(performingUser, RoleConstants.OwnerRoleName);
                if (isSystemDeveloper || isSystemOwner)
                {
                    company.Enabled = dto.Enabled.Value;
                }
                else if (company.Enabled != dto.Enabled.Value)
                {
                    _logger.LogWarning("Usuário {UserId} (não admin de sistema) tentou alterar o status 'Enabled' da empresa {CompanyId} via PUT. Esta ação é restrita ou deve ser feita via PATCH /activate ou /inactivate.", performingUserId, companyId);
                    // Para evitar que um Admin de empresa (que é IsCompanyOwner) mude o status aqui,
                    // a alteração de 'Enabled' via PUT fica restrita aos donos do sistema.
                    // Se a intenção é que o Admin da empresa (IsCompanyOwner) possa mudar aqui também, ajuste a condição.
                }
            }

            await _unitOfWork.Companies.Update(company);
            await _unitOfWork.CommitAsync();
            _logger.LogInformation("Empresa {CompanyId} atualizada com sucesso pelo usuário {UserId}.", company.Id, performingUserId);

            return _mapper.Map<CompanyDto>(company);
        }

        public async Task<CompanyDto> GetCompanyByIdAsync(Guid companyId, ClaimsPrincipal performingUserPrincipal)
        {
            var company = await _unitOfWork.Companies.GetByIdAsync(companyId);
            if (company == null)
            {
                _logger.LogWarning("Empresa com ID {CompanyId} não encontrada.", companyId);
                throw new NotFoundException("Empresa não encontrada.", "COMPANY_NOT_FOUND");
            }

            var performingUserId = performingUserPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
            var performingUser = await _userManager.FindByIdAsync(performingUserId ?? "");

            if (performingUser == null)
                throw new PermissionDeniedException("Usuário requisitante inválido.", "PERFORMING_USER_NOT_FOUND");

            bool isAssociatedWithCompanyOrIsAdmin = performingUser.CompanyId == companyId &&
                                                   (performingUser.IsCompanyOwner || await _userManager.IsInRoleAsync(performingUser, RoleConstants.AdminRoleName));
            bool isSystemDeveloper = await _userManager.IsInRoleAsync(performingUser, RoleConstants.DeveloperRoleName);
            bool isSystemOwner = await _userManager.IsInRoleAsync(performingUser, RoleConstants.OwnerRoleName);

            if (!isAssociatedWithCompanyOrIsAdmin && !isSystemDeveloper && !isSystemOwner)
            {
                _logger.LogWarning("Usuário {UserId} não autorizado a visualizar a empresa {CompanyId}.", performingUserId, companyId);
                throw new PermissionDeniedException("Você não tem permissão para visualizar esta empresa.", "COMPANY_VIEW_FORBIDDEN");
            }

            return _mapper.Map<CompanyDto>(company);
        }

        public async Task InactivateCompanyAsync(Guid companyId, ClaimsPrincipal performingUserPrincipal)
        {
            var company = await _unitOfWork.Companies.GetByIdAsync(companyId);
            if (company == null)
            {
                _logger.LogWarning("Empresa com ID {CompanyId} não encontrada para inativação.", companyId);
                throw new NotFoundException("Empresa não encontrada.", "COMPANY_NOT_FOUND");
            }

            if (!company.Enabled)
            {
                _logger.LogInformation("Empresa {CompanyId} já está inativa.", companyId);
                throw new ConflictException("A empresa já está inativa.", "COMPANY_ALREADY_INACTIVE");
            }

            var performingUserId = performingUserPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
            var performingUser = await _userManager.FindByIdAsync(performingUserId ?? "");

            if (!await CanManageCompanyAsync(performingUser, companyId))
            {
                _logger.LogWarning("Usuário {UserId} não autorizado a inativar a empresa {CompanyId}.", performingUserId, companyId);
                throw new PermissionDeniedException("Você não tem permissão para inativar esta empresa.", "COMPANY_INACTIVATE_FORBIDDEN");
            }

            company.Enabled = false;
            company.LastUpdate = DateTime.UtcNow;

            await _unitOfWork.Companies.Update(company);
            await _unitOfWork.CommitAsync();
            _logger.LogInformation("Empresa {CompanyId} inativada com sucesso pelo usuário {UserId}.", company.Id, performingUserId);
        }

        public async Task ActivateCompanyAsync(Guid companyId, ClaimsPrincipal performingUserPrincipal)
        {
            var company = await _unitOfWork.Companies.GetByIdAsync(companyId);
            if (company == null)
            {
                _logger.LogWarning("Empresa com ID {CompanyId} não encontrada para ativação.", companyId);
                throw new NotFoundException("Empresa não encontrada.", "COMPANY_NOT_FOUND");
            }

            if (company.Enabled)
            {
                _logger.LogInformation("Empresa {CompanyId} já está ativa.", companyId);
                throw new ConflictException("A empresa já está ativa.", "COMPANY_ALREADY_ACTIVE");
            }

            var performingUserId = performingUserPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
            var performingUser = await _userManager.FindByIdAsync(performingUserId ?? "");

            if (!await CanManageCompanyAsync(performingUser, companyId))
            {
                _logger.LogWarning("Usuário {UserId} não autorizado a ativar a empresa {CompanyId}.", performingUserId, companyId);
                throw new PermissionDeniedException("Você não tem permissão para ativar esta empresa.", "COMPANY_ACTIVATE_FORBIDDEN");
            }

            company.Enabled = true;
            company.LastUpdate = DateTime.UtcNow;

            await _unitOfWork.Companies.Update(company);
            await _unitOfWork.CommitAsync();
            _logger.LogInformation("Empresa {CompanyId} ativada com sucesso pelo usuário {UserId}.", company.Id, performingUserId);
        }

        public async Task<IEnumerable<CompanyDto>> GetAllCompaniesAsync(ClaimsPrincipal performingUserPrincipal)
        {
            var performingUserId = performingUserPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
            var performingUser = await _userManager.FindByIdAsync(performingUserId ?? "");

            if (performingUser == null)
            {
                _logger.LogWarning("Tentativa de GetAllCompanies por usuário não encontrado (ID: {UserId}).", performingUserId);
                throw new PermissionDeniedException("Usuário requisitante inválido.", "PERFORMING_USER_NOT_FOUND");
            }

            bool isSystemDeveloper = await _userManager.IsInRoleAsync(performingUser, RoleConstants.DeveloperRoleName);
            bool isSystemOwner = await _userManager.IsInRoleAsync(performingUser, RoleConstants.OwnerRoleName);

            if (!isSystemDeveloper && !isSystemOwner)
            {
                _logger.LogWarning("Usuário {UserId} (Roles: {Roles}) não autorizado a listar todas as empresas.",
                    performingUserId, string.Join(",", await _userManager.GetRolesAsync(performingUser)));
                throw new PermissionDeniedException("Você não tem permissão para listar todas as empresas.", "GET_ALL_COMPANIES_FORBIDDEN");
            }

            var companies = await _unitOfWork.Companies.GetAllAsync();
            _logger.LogInformation("Usuário {UserId} listou todas as {Count} empresas.", performingUserId, companies.Count());
            return _mapper.Map<IEnumerable<CompanyDto>>(companies);
        }
    }
}
