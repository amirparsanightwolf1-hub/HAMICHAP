using HamiChap_MES_ERP.Services;
using Microsoft.AspNetCore.Authorization;

namespace HamiChap_MES_ERP.Services;

public sealed class InventoryPermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public sealed class InventoryPermissionHandler(PermissionService permissions)
    : AuthorizationHandler<InventoryPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        InventoryPermissionRequirement requirement)
    {
        if (await permissions.HasPermissionAsync(context.User, requirement.Permission))
            context.Succeed(requirement);
    }
}
