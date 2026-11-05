using System.Security.Claims;
using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Administration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

/// <summary>
/// Performs permission and role-delegation queries using a fresh
/// ApplicationDbContext per operation. This is important in Blazor Server,
/// where a scoped DbContext can otherwise be shared by concurrent component
/// operations in the same circuit.
/// </summary>
public class PermissionService
{
    private const string AdministratorRole = "Administrator";
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public PermissionService(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    // ============================================================
    // ROLE MANAGEMENT
    // ============================================================

    /// <summary>
    /// Returns the roles that the current user is allowed to manage.
    /// Administrators can manage every role. Other users can only manage
    /// roles explicitly assigned through RolePermissionManagement.
    /// </summary>
    public async Task<List<IdentityRole>> GetManageableRolesAsync(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
            return new List<IdentityRole>();

        await using var db = await _dbFactory.CreateDbContextAsync();

        if (user.IsInRole(AdministratorRole))
        {
            return await db.Roles
                .OrderBy(x => x.Name)
                .ToListAsync();
        }

        var managerRoleIds = await GetCurrentUserRoleIdsAsync(db, user);
        if (managerRoleIds.Count == 0)
            return new List<IdentityRole>();

        var targetRoleIds = await db.RolePermissionManagements
            .Where(x => managerRoleIds.Contains(x.ManagerRoleId))
            .Select(x => x.TargetRoleId)
            .Distinct()
            .ToListAsync();

        if (targetRoleIds.Count == 0)
            return new List<IdentityRole>();

        return await db.Roles
            .Where(x => targetRoleIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    /// <summary>Determines whether the current user can manage a target role.</summary>
    public async Task<bool> CanManageRoleAsync(ClaimsPrincipal user, string targetRoleId)
    {
        if (user.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(targetRoleId))
            return false;

        if (user.IsInRole(AdministratorRole))
            return true;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var managerRoleIds = await GetCurrentUserRoleIdsAsync(db, user);
        if (managerRoleIds.Count == 0)
            return false;

        return await db.RolePermissionManagements.AnyAsync(x =>
            managerRoleIds.Contains(x.ManagerRoleId) && x.TargetRoleId == targetRoleId);
    }

    // ============================================================
    // PERMISSION DELEGATION
    // ============================================================

    /// <summary>Returns the permissions the user may delegate to a target role.</summary>
    public async Task<List<Permission>> GetDelegatablePermissionsAsync(
        ClaimsPrincipal user,
        string targetRoleId)
    {
        if (user.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(targetRoleId))
            return new List<Permission>();

        await using var db = await _dbFactory.CreateDbContextAsync();

        if (user.IsInRole(AdministratorRole))
        {
            return await db.Permissions
                .OrderBy(x => x.Name)
                .ToListAsync();
        }

        var managerRoleIds = await GetCurrentUserRoleIdsAsync(db, user);
        if (managerRoleIds.Count == 0)
            return new List<Permission>();

        return await db.RolePermissionManagements
            .Where(x => managerRoleIds.Contains(x.ManagerRoleId) && x.TargetRoleId == targetRoleId)
            .Select(x => x.Permission)
            .Distinct()
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    /// <summary>Gets permission IDs currently assigned to a role, if the user may manage it.</summary>
    public async Task<HashSet<int>> GetRolePermissionIdsAsync(
        ClaimsPrincipal user,
        string targetRoleId)
    {
        if (user.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(targetRoleId))
            return new HashSet<int>();

        await using var db = await _dbFactory.CreateDbContextAsync();
        if (!await CanManageRoleAsync(user, targetRoleId))
            return new HashSet<int>();

        var permissionIds = await db.RolePermissions
            .Where(x => x.RoleId == targetRoleId)
            .Select(x => x.PermissionId)
            .ToListAsync();

        return permissionIds.ToHashSet();
    }

    /// <summary>
    /// Saves only permissions that the current user is authorized to delegate.
    /// Client-supplied permission IDs are never trusted on their own.
    /// </summary>
    public async Task<bool> SaveRolePermissionsAsync(
        ClaimsPrincipal user,
        string targetRoleId,
        IEnumerable<int> requestedPermissionIds)
    {
        if (user.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(targetRoleId))
            return false;

        if (!await CanManageRoleAsync(user, targetRoleId))
            return false;

        var manageablePermissions = await GetDelegatablePermissionsAsync(user, targetRoleId);
        var manageablePermissionIds = manageablePermissions.Select(x => x.Id).ToHashSet();
        var authorizedRequestedIds = requestedPermissionIds
            .Distinct()
            .Where(manageablePermissionIds.Contains)
            .ToHashSet();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var existing = await db.RolePermissions
            .Where(x => x.RoleId == targetRoleId)
            .ToListAsync();

        var existingPermissionIds = existing.Select(x => x.PermissionId).ToHashSet();

        var permissionsToRemove = existing
            .Where(x => manageablePermissionIds.Contains(x.PermissionId)
                        && !authorizedRequestedIds.Contains(x.PermissionId))
            .ToList();

        db.RolePermissions.RemoveRange(permissionsToRemove);

        foreach (var permissionId in authorizedRequestedIds)
        {
            if (!existingPermissionIds.Contains(permissionId))
            {
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = targetRoleId,
                    PermissionId = permissionId
                });
            }
        }

        await db.SaveChangesAsync();
        return true;
    }

    // ============================================================
    // GENERAL PERMISSION CHECKING
    // ============================================================

    /// <summary>
    /// Determines whether the current user has a permission through any role.
    /// Administrators automatically have every permission.
    /// </summary>
    public async Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permissionName)
    {
        if (user.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(permissionName))
            return false;

        if (user.IsInRole(AdministratorRole))
            return true;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var roleIds = await GetCurrentUserRoleIdsAsync(db, user);
        if (roleIds.Count == 0)
            return false;

        return await db.RolePermissions.AnyAsync(rp =>
            roleIds.Contains(rp.RoleId) && rp.Permission.Name == permissionName);
    }

    // ============================================================
    // INTERNAL HELPERS
    // ============================================================

    /// <summary>
    /// Reads role IDs directly from the Identity tables using the operation's
    /// own DbContext. This avoids UserManager/RoleManager sharing the circuit's
    /// scoped Identity DbContext with other concurrent component operations.
    /// </summary>
    private static async Task<List<string>> GetCurrentUserRoleIdsAsync(
        ApplicationDbContext db,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return new List<string>();

        var roleIds = await db.UserRoles
            .Where(userRole => userRole.UserId == userId)
            .Select(userRole => userRole.RoleId)
            .Distinct()
            .ToListAsync();

        return roleIds;
    }
}
