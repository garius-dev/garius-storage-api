using Microsoft.AspNetCore.Authorization;

namespace GariusStorage.Api.Extensions
{
    public static class AuthorizationPolicies
    {
        public static void ConfigurePolicies(AuthorizationOptions options)
        {
            options.AddPolicy("LoggedInOnly", policy =>
            policy.RequireAuthenticatedUser());
        }
    }
}
