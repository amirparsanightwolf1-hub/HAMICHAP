using Microsoft.AspNetCore.Authorization;

namespace HamiChap_MES_ERP.Services;

public sealed record EmployeePermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class EmployeePermissionHandler(PermissionService permissions)
    : AuthorizationHandler<EmployeePermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        EmployeePermissionRequirement requirement)
    {
        if (await permissions.HasPermissionAsync(context.User, requirement.Permission))
            context.Succeed(requirement);
    }
}
