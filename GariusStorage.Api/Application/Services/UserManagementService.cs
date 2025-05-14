using GariusStorage.Api.Application.Dtos;
using GariusStorage.Api.Application.Interfaces;
using GariusStorage.Api.Domain.Constants;
using GariusStorage.Api.Domain.Entities.Identity;
using GariusStorage.Api.Infrastructure.Data; // Necessário para ApplicationDbContext
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore; // Necessário para ToListAsync, Include, etc.
using System.Security.Claims;

namespace GariusStorage.Api.Application.Services
{
    public class UserManagementService : IUserManagementService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ApplicationDbContext _context; // Injetar o DbContext
        private readonly ILogger<UserManagementService> _logger;

        public UserManagementService(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            ApplicationDbContext context, // Adicionar DbContext à injeção
            ILogger<UserManagementService> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context; // Atribuir o DbContext
            _logger = logger;
        }

        private async Task<ApplicationUser?> FindUserEntityAsync(string emailOrUsername)
        {
            if (emailOrUsername.Contains("@"))
            {
                return await _userManager.FindByEmailAsync(emailOrUsername);
            }
            return await _userManager.FindByNameAsync(emailOrUsername);
        }

        public async Task<UserProfileDto?> GetUserProfileAsync(string emailOrUsername)
        {
            var userQuery = _context.Users.AsQueryable();

            if (emailOrUsername.Contains("@"))
            {
                userQuery = userQuery.Where(u => u.NormalizedEmail == emailOrUsername.ToUpperInvariant());
            }
            else
            {
                userQuery = userQuery.Where(u => u.NormalizedUserName == emailOrUsername.ToUpperInvariant());
            }

            var userProfile = await userQuery
                .Select(u => new UserProfileDto
                {
                    UserId = u.Id.ToString(),
                    UserName = u.UserName,
                    Email = u.Email,
                    Roles = (from ur in _context.UserRoles
                             join r in _context.Roles on ur.RoleId equals r.Id
                             where ur.UserId == u.Id
                             select r.Name).ToList(),
                    Claims = _context.UserClaims
                               .Where(uc => uc.UserId == u.Id)
                               .Select(uc => new ClaimDto { Type = uc.ClaimType, Value = uc.ClaimValue })
                               .ToList()
                })
                .FirstOrDefaultAsync();

            if (userProfile == null)
            {
                _logger.LogWarning("Usuário não encontrado ao tentar obter perfil: {EmailOrUsername}", emailOrUsername);
                return null;
            }

            return userProfile;
        }

        public async Task<UpdateUserProfileResultDto> UpdateUserProfileAsync(
            string targetEmailOrUsername,
            UserProfileDto profileUpdateDto,
            ClaimsPrincipal performingUserPrincipal)
        {
            var targetUser = await FindUserEntityAsync(targetEmailOrUsername);
            if (targetUser == null)
            {
                _logger.LogWarning("Usuário alvo não encontrado para atualização de perfil: {TargetEmailOrUsername}", targetEmailOrUsername);
                return UpdateUserProfileResultDto.Failed($"Usuário '{targetEmailOrUsername}' não encontrado.");
            }

            var performingUserId = _userManager.GetUserId(performingUserPrincipal);
            if (string.IsNullOrEmpty(performingUserId))
            {
                _logger.LogError("Não foi possível obter o ID do usuário que está realizando a operação.");
                return UpdateUserProfileResultDto.Failed("Não autorizado: Não foi possível identificar o usuário requisitante.");
            }

            var performingUser = await _userManager.FindByIdAsync(performingUserId);
            if (performingUser == null)
            {
                _logger.LogError("Usuário requisitante com ID {PerformingUserId} não encontrado no banco de dados.", performingUserId);
                return UpdateUserProfileResultDto.Failed("Não autorizado: Usuário requisitante inválido.");
            }

            if (targetUser.Id.ToString() == performingUserId)
            {
                _logger.LogWarning("Usuário {PerformingUserEmail} tentou modificar seu próprio perfil via endpoint de gerenciamento.", performingUser.Email);
                return UpdateUserProfileResultDto.Failed("Não é permitido modificar o próprio perfil através deste endpoint. Use os endpoints de perfil pessoal.");
            }

            var performingUserRoles = await _userManager.GetRolesAsync(performingUser);

            var targetUserCurrentRoles = await _userManager.GetRolesAsync(targetUser);


            bool canUpdate = CheckHierarchicalPermission(performingUserRoles, targetUserCurrentRoles);

            if (!canUpdate)
            {
                _logger.LogWarning("Usuário {PerformingUserEmail} (Roles: {PerformingUserRoles}) não tem permissão para modificar o usuário {TargetUserEmail} (Roles: {TargetUserRoles}).",
                    performingUser.Email, string.Join(",", performingUserRoles), targetUser.Email, string.Join(",", targetUserCurrentRoles));
                return UpdateUserProfileResultDto.Failed("Não autorizado: Você não tem permissão para modificar este usuário.");
            }

            var rolesToAdd = profileUpdateDto.Roles.Except(targetUserCurrentRoles).ToList();
            var rolesToRemove = targetUserCurrentRoles.Except(profileUpdateDto.Roles).ToList();

            foreach (var roleName in rolesToAdd.Concat(profileUpdateDto.Roles).Distinct())
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    _logger.LogWarning("Tentativa de atribuir role inexistente '{RoleName}' ao usuário {TargetUserEmail}.", roleName, targetUser.Email);
                    return UpdateUserProfileResultDto.Failed($"A role '{roleName}' não existe.");
                }
            }

            if (!CanAssignRoles(performingUserRoles, profileUpdateDto.Roles))
            {
                _logger.LogWarning("Usuário {PerformingUserEmail} tentou atribuir roles que não pode gerenciar ao usuário {TargetUserEmail}.", performingUser.Email, targetUser.Email);
                return UpdateUserProfileResultDto.Failed("Não autorizado: Você não pode atribuir algumas das roles especificadas.");
            }

            IdentityResult rolesUpdateResult;
            if (rolesToAdd.Any())
            {
                rolesUpdateResult = await _userManager.AddToRolesAsync(targetUser, rolesToAdd);
                if (!rolesUpdateResult.Succeeded)
                {
                    _logger.LogError("Falha ao adicionar roles ao usuário {TargetUserEmail}. Erros: {Errors}", targetUser.Email, string.Join(", ", rolesUpdateResult.Errors.Select(e => e.Description)));
                    return UpdateUserProfileResultDto.Failed("Falha ao atualizar roles.", rolesUpdateResult.Errors.Select(e => e.Description));
                }
            }

            if (rolesToRemove.Any())
            {
                rolesUpdateResult = await _userManager.RemoveFromRolesAsync(targetUser, rolesToRemove);
                if (!rolesUpdateResult.Succeeded)
                {
                    _logger.LogError("Falha ao remover roles do usuário {TargetUserEmail}. Erros: {Errors}", targetUser.Email, string.Join(", ", rolesUpdateResult.Errors.Select(e => e.Description)));
                    return UpdateUserProfileResultDto.Failed("Falha ao atualizar roles.", rolesUpdateResult.Errors.Select(e => e.Description));
                }
            }

            var currentClaims = await _userManager.GetClaimsAsync(targetUser);
            var claimsFromDto = profileUpdateDto.Claims.Select(c => new System.Security.Claims.Claim(c.Type, c.Value)).ToList();

            var claimsToAdd = claimsFromDto.Where(dtoClaim => !currentClaims.Any(currClaim => currClaim.Type == dtoClaim.Type && currClaim.Value == dtoClaim.Value)).ToList();
            var claimsToRemove = currentClaims.Where(currClaim => !claimsFromDto.Any(dtoClaim => dtoClaim.Type == currClaim.Type && dtoClaim.Value == currClaim.Value)).ToList();

            if (claimsToRemove.Any())
            {
                var claimsRemoveResult = await _userManager.RemoveClaimsAsync(targetUser, claimsToRemove);
                if (!claimsRemoveResult.Succeeded)
                {
                    _logger.LogError("Falha ao remover claims do usuário {TargetUserEmail}. Erros: {Errors}", targetUser.Email, string.Join(", ", claimsRemoveResult.Errors.Select(e => e.Description)));
                    return UpdateUserProfileResultDto.Failed("Falha ao atualizar claims.", claimsRemoveResult.Errors.Select(e => e.Description));
                }
            }

            if (claimsToAdd.Any())
            {
                var claimsAddResult = await _userManager.AddClaimsAsync(targetUser, claimsToAdd);
                if (!claimsAddResult.Succeeded)
                {
                    _logger.LogError("Falha ao adicionar claims ao usuário {TargetUserEmail}. Erros: {Errors}", targetUser.Email, string.Join(", ", claimsAddResult.Errors.Select(e => e.Description)));
                    return UpdateUserProfileResultDto.Failed("Falha ao atualizar claims.", claimsAddResult.Errors.Select(e => e.Description));
                }
            }

            targetUser.LastUpdate = DateTime.UtcNow;
            var updateResult = await _userManager.UpdateAsync(targetUser);
            if (!updateResult.Succeeded)
            {
                _logger.LogError("Falha ao atualizar a entidade do usuário {TargetUserEmail} (ex: LastUpdate). Erros: {Errors}", targetUser.Email, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                // Decide se isso é um erro fatal para a operação ou apenas um aviso.
                // Por ora, vamos considerar que a atualização das roles/claims é o principal.
            }


            _logger.LogInformation("Perfil do usuário {TargetUserEmail} atualizado com sucesso por {PerformingUserEmail}.", targetUser.Email, performingUser.Email);
            var updatedProfile = await GetUserProfileAsync(targetEmailOrUsername);
            return UpdateUserProfileResultDto.Success(updatedProfile!, "Perfil do usuário atualizado com sucesso.");
        }

        private bool CheckHierarchicalPermission(IList<string> performingUserRoles, IList<string> targetUserRoles)
        {
            bool performingIsDeveloper = performingUserRoles.Contains(RoleConstants.DeveloperRoleName);
            bool performingIsOwner = performingUserRoles.Contains(RoleConstants.OwnerRoleName);
            bool performingIsAdmin = performingUserRoles.Contains(RoleConstants.AdminRoleName);

            bool targetIsDeveloper = targetUserRoles.Contains(RoleConstants.DeveloperRoleName);
            bool targetIsOwner = targetUserRoles.Contains(RoleConstants.OwnerRoleName);

            if (performingIsDeveloper) return true;

            if (performingIsOwner)
            {
                return !targetIsDeveloper;
            }

            if (performingIsAdmin)
            {
                return !targetIsDeveloper && !targetIsOwner;
            }

            return false;
        }

        private bool CanAssignRoles(IList<string> performingUserRoles, List<string> rolesToAssign)
        {
            if (performingUserRoles.Contains(RoleConstants.DeveloperRoleName)) return true;

            if (performingUserRoles.Contains(RoleConstants.OwnerRoleName))
            {
                if (rolesToAssign.Contains(RoleConstants.DeveloperRoleName)) return false;
                return true;
            }

            if (performingUserRoles.Contains(RoleConstants.AdminRoleName))
            {
                if (rolesToAssign.Contains(RoleConstants.DeveloperRoleName) || rolesToAssign.Contains(RoleConstants.OwnerRoleName)) return false;
                return true;
            }

            return !rolesToAssign.Any();
        }
    }
}
