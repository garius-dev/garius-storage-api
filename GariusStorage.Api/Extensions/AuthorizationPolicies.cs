using GariusStorage.Api.Domain.Constants;
using Microsoft.AspNetCore.Authorization;

namespace GariusStorage.Api.Extensions
{
    public static class AuthorizationPolicies
    {
        public const string RequireAdminRole = "RequireAdminRole";
        public const string RequireOwnerRole = "RequireOwnerRole";
        public const string RequireDeveloperRole = "RequireDeveloperRole";
        public const string RequireAdminOrOwnerRole = "RequireAdminOrOwnerRole";

        public static void ConfigurePolicies(AuthorizationOptions options)
        {
            options.AddPolicy("LoggedInOnly", policy =>
            policy.RequireAuthenticatedUser());

            options.AddPolicy(RequireAdminRole, policy =>
                policy.RequireRole(RoleConstants.AdminRoleName).RequireAuthenticatedUser()); //

            options.AddPolicy(RequireOwnerRole, policy =>
                policy.RequireRole(RoleConstants.OwnerRoleName).RequireAuthenticatedUser()); //

            options.AddPolicy(RequireDeveloperRole, policy =>
                policy.RequireRole(RoleConstants.DeveloperRoleName).RequireAuthenticatedUser()); //

            options.AddPolicy(RequireAdminOrOwnerRole, policy =>
                policy.RequireRole(RoleConstants.AdminRoleName, RoleConstants.OwnerRoleName).RequireAuthenticatedUser()); //
        }
    }
}
