using System.Security.Claims;
using HamiChap_MES_ERP.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HamiChap_MES_ERP.Services;

// A policy is "Permission:<seeded permission>" or a | separated list of alternatives.
public sealed record EmployeeAccessRequirement(string? Permissions) : IAuthorizationRequirement;

public sealed class EmployeePolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        const string prefix = "Permission:";
        if (!policyName.StartsWith(prefix, StringComparison.Ordinal))
            return base.GetPolicyAsync(policyName);
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new EmployeeAccessRequirement(policyName[prefix.Length..]))
            .Build();
        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}

public sealed class EmployeeAccessHandler(
    IDbContextFactory<ApplicationDbContext> factory, PermissionService permissions)
    : AuthorizationHandler<EmployeeAccessRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, EmployeeAccessRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;
        var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(id)) return;
        await using var db = await factory.CreateDbContextAsync();
        if (!await db.Users.AnyAsync(x => x.Id == id && x.IsActive)) return;
        if (requirement.Permissions is null)
        {
            context.Succeed(requirement);
            return;
        }
        foreach (var permission in requirement.Permissions.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (await permissions.HasPermissionAsync(context.User, permission))
            {
                context.Succeed(requirement);
                return;
            }
    }
}
