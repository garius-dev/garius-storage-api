using GariusStorage.Api.Application.Dtos;
using GariusStorage.Api.Application.Interfaces;
using GariusStorage.Api.Domain.Constants;
using GariusStorage.Api.Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace GariusStorage.Api.Application.Services
{
    public class UserManagementService : IUserManagementService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager; // Necessário para validar roles
        private readonly ILogger<UserManagementService> _logger;

        public UserManagementService(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            ILogger<UserManagementService> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        private async Task<ApplicationUser?> FindUserAsync(string emailOrUsername)
        {
            ApplicationUser? user = null;
            if (emailOrUsername.Contains("@"))
            {
                user = await _userManager.FindByEmailAsync(emailOrUsername);
            }
            else
            {
                user = await _userManager.FindByNameAsync(emailOrUsername);
            }
            return user;
        }

        public async Task<UserProfileDto?> GetUserProfileAsync(string emailOrUsername)
        {
            var user = await FindUserAsync(emailOrUsername);
            if (user == null)
            {
                _logger.LogWarning("Usuário não encontrado ao tentar obter perfil: {EmailOrUsername}", emailOrUsername);
                return null;
            }

            var roles = await _userManager.GetRolesAsync(user);
            var claims = await _userManager.GetClaimsAsync(user);

            return new UserProfileDto
            {
                UserId = user.Id.ToString(),
                UserName = user.UserName,
                Email = user.Email,
                Roles = roles.ToList(),
                Claims = claims.Select(c => new ClaimDto { Type = c.Type, Value = c.Value }).ToList()
            };
        }

        public async Task<UpdateUserProfileResultDto> UpdateUserProfileAsync(
            string targetEmailOrUsername,
            UserProfileDto profileUpdateDto,
            ClaimsPrincipal performingUserPrincipal)
        {
            var targetUser = await FindUserAsync(targetEmailOrUsername);
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

            // Regra: Não permitir auto-modificação por este endpoint para evitar escalação simples.
            if (targetUser.Id.ToString() == performingUserId)
            {
                _logger.LogWarning("Usuário {PerformingUserEmail} tentou modificar seu próprio perfil via endpoint de gerenciamento.", performingUser.Email);
                return UpdateUserProfileResultDto.Failed("Não é permitido modificar o próprio perfil através deste endpoint. Use os endpoints de perfil pessoal.");
            }

            // Lógica de Autorização Hierárquica
            var performingUserRoles = await _userManager.GetRolesAsync(performingUser);
            var targetUserRoles = await _userManager.GetRolesAsync(targetUser);

            bool canUpdate = CheckHierarchicalPermission(performingUserRoles, targetUserRoles);
            if (!canUpdate)
            {
                _logger.LogWarning("Usuário {PerformingUserEmail} (Roles: {PerformingUserRoles}) não tem permissão para modificar o usuário {TargetUserEmail} (Roles: {TargetUserRoles}).",
                    performingUser.Email, string.Join(",", performingUserRoles), targetUser.Email, string.Join(",", targetUserRoles));
                return UpdateUserProfileResultDto.Failed("Não autorizado: Você não tem permissão para modificar este usuário.");
            }

            // Sincronizar Roles
            var currentRoles = await _userManager.GetRolesAsync(targetUser);
            var rolesToAdd = profileUpdateDto.Roles.Except(currentRoles).ToList();
            var rolesToRemove = currentRoles.Except(profileUpdateDto.Roles).ToList();

            // Validação de roles antes de adicionar
            foreach (var roleName in rolesToAdd.Concat(profileUpdateDto.Roles).Distinct()) // Verifica todas as roles desejadas
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    _logger.LogWarning("Tentativa de atribuir role inexistente '{RoleName}' ao usuário {TargetUserEmail}.", roleName, targetUser.Email);
                    return UpdateUserProfileResultDto.Failed($"A role '{roleName}' não existe.");
                }
            }
            // Não permitir que um usuário atribua uma role que ele mesmo não poderia gerenciar (proteção adicional)
            if (!CanAssignRoles(performingUserRoles, profileUpdateDto.Roles))
            {
                _logger.LogWarning("Usuário {PerformingUserEmail} tentou atribuir roles que não pode gerenciar ao usuário {TargetUserEmail}.", performingUser.Email, targetUser.Email);
                return UpdateUserProfileResultDto.Failed("Não autorizado: Você não pode atribuir algumas das roles especificadas.");
            }


            var rolesUpdateResult = await _userManager.AddToRolesAsync(targetUser, rolesToAdd);
            if (!rolesUpdateResult.Succeeded)
            {
                _logger.LogError("Falha ao adicionar roles ao usuário {TargetUserEmail}. Erros: {Errors}", targetUser.Email, string.Join(", ", rolesUpdateResult.Errors.Select(e => e.Description)));
                return UpdateUserProfileResultDto.Failed("Falha ao atualizar roles.", rolesUpdateResult.Errors.Select(e => e.Description));
            }

            rolesUpdateResult = await _userManager.RemoveFromRolesAsync(targetUser, rolesToRemove);
            if (!rolesUpdateResult.Succeeded)
            {
                _logger.LogError("Falha ao remover roles do usuário {TargetUserEmail}. Erros: {Errors}", targetUser.Email, string.Join(", ", rolesUpdateResult.Errors.Select(e => e.Description)));
                return UpdateUserProfileResultDto.Failed("Falha ao atualizar roles.", rolesUpdateResult.Errors.Select(e => e.Description));
            }

            // Sincronizar Claims
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
            await _userManager.UpdateAsync(targetUser);

            _logger.LogInformation("Perfil do usuário {TargetUserEmail} atualizado com sucesso por {PerformingUserEmail}.", targetUser.Email, performingUser.Email);
            var updatedProfile = await GetUserProfileAsync(targetEmailOrUsername); // Pega o perfil atualizado
            return UpdateUserProfileResultDto.Success(updatedProfile!);
        }

        private bool CheckHierarchicalPermission(IList<string> performingUserRoles, IList<string> targetUserRoles)
        {
            bool performingIsDeveloper = performingUserRoles.Contains(RoleConstants.DeveloperRoleName);
            bool performingIsOwner = performingUserRoles.Contains(RoleConstants.OwnerRoleName);
            bool performingIsAdmin = performingUserRoles.Contains(RoleConstants.AdminRoleName);

            bool targetIsDeveloper = targetUserRoles.Contains(RoleConstants.DeveloperRoleName);
            bool targetIsOwner = targetUserRoles.Contains(RoleConstants.OwnerRoleName);
            // bool targetIsAdmin = targetUserRoles.Contains(RoleConstants.AdminRoleName); // Não precisamos checar o admin do alvo para a regra de quem pode editar

            if (performingIsDeveloper) return true; // Developer pode editar qualquer um

            if (performingIsOwner)
            {
                return !targetIsDeveloper; // Owner pode editar qualquer um, exceto Developer
            }

            if (performingIsAdmin)
            {
                return !targetIsDeveloper && !targetIsOwner; // Admin pode editar qualquer um, exceto Developer e Owner
            }

            return false; // Usuário padrão não pode editar ninguém por este serviço
        }

        private bool CanAssignRoles(IList<string> performingUserRoles, List<string> rolesToAssign)
        {
            // Se o usuário requisitante for Developer, ele pode atribuir qualquer role.
            if (performingUserRoles.Contains(RoleConstants.DeveloperRoleName)) return true;

            // Se o requisitante for Owner, ele não pode atribuir a role Developer.
            if (performingUserRoles.Contains(RoleConstants.OwnerRoleName))
            {
                if (rolesToAssign.Contains(RoleConstants.DeveloperRoleName)) return false;
                return true;
            }

            // Se o requisitante for Admin, ele não pode atribuir as roles Developer ou Owner.
            if (performingUserRoles.Contains(RoleConstants.AdminRoleName))
            {
                if (rolesToAssign.Contains(RoleConstants.DeveloperRoleName) || rolesToAssign.Contains(RoleConstants.OwnerRoleName)) return false;
                return true;
            }

            // Se o usuário não for Developer, Owner ou Admin, ele não pode atribuir nenhuma role por este mecanismo.
            // (A menos que haja uma lógica mais granular, mas para este caso, vamos simplificar)
            // Se a lista de roles a atribuir não estiver vazia e o usuário não for um dos acima, ele não pode.
            return !rolesToAssign.Any();
        }
    }
}
