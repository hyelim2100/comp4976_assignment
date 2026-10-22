using Microsoft.AspNetCore.Authorization;

namespace MunicipalElections.Services;

public static class AdminPolicies
{
    // Super Admin or a member of any municipality admin group.
    public const string AdminAccess = "AdminAccess";
}

public class AdminAccessRequirement : IAuthorizationRequirement;

public class AdminAccessHandler(MunicipalityAccess access) : AuthorizationHandler<AdminAccessRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminAccessRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true && await access.IsAdminAsync())
        {
            context.Succeed(requirement);
        }
    }
}
