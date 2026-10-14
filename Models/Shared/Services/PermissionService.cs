using System.Security.Claims;
using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Administration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

public class PermissionService
{
    private const string AdministratorRole = "Administrator";

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public PermissionService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
    }


    // ============================================================
    // ROLE MANAGEMENT
    // ============================================================

    /// <summary>
    /// Returns the roles that the current user is allowed
    /// to manage.
    ///
    /// Administrator can manage every role.
    /// Other users can only manage roles explicitly assigned
    /// through RolePermissionManagement.
    /// </summary>
    public async Task<List<IdentityRole>> GetManageableRolesAsync(
        ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
            return new List<IdentityRole>();


        if (user.IsInRole(AdministratorRole))
        {
            return await _roleManager.Roles
                .OrderBy(x => x.Name)
                .ToListAsync();
        }


        var managerRoleIds =
            await GetCurrentUserRoleIdsAsync(user);

        if (!managerRoleIds.Any())
            return new List<IdentityRole>();


        var targetRoleIds =
            await _db.RolePermissionManagements
                .Where(x =>
                    managerRoleIds.Contains(
                        x.ManagerRoleId))
                .Select(x => x.TargetRoleId)
                .Distinct()
                .ToListAsync();


        if (!targetRoleIds.Any())
            return new List<IdentityRole>();


        return await _roleManager.Roles
            .Where(x =>
                targetRoleIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .ToListAsync();
    }


    /// <summary>
    /// Determines whether the current user can manage
    /// the specified target role.
    /// </summary>
    public async Task<bool> CanManageRoleAsync(
        ClaimsPrincipal user,
        string targetRoleId)
    {
        if (user.Identity?.IsAuthenticated != true)
            return false;

        if (string.IsNullOrWhiteSpace(targetRoleId))
            return false;


        // Administrator bypasses delegation restrictions.
        if (user.IsInRole(AdministratorRole))
            return true;


        var managerRoleIds =
            await GetCurrentUserRoleIdsAsync(user);

        if (!managerRoleIds.Any())
            return false;


        return await _db.RolePermissionManagements
            .AnyAsync(x =>
                managerRoleIds.Contains(
                    x.ManagerRoleId)
                &&
                x.TargetRoleId == targetRoleId);
    }


    // ============================================================
    // PERMISSION DELEGATION
    // ============================================================

    /// <summary>
    /// Returns the permissions that the current user is allowed
    /// to delegate to the specified target role.
    /// </summary>
    public async Task<List<Permission>>
        GetDelegatablePermissionsAsync(
            ClaimsPrincipal user,
            string targetRoleId)
    {
        if (user.Identity?.IsAuthenticated != true)
            return new List<Permission>();

        if (string.IsNullOrWhiteSpace(targetRoleId))
            return new List<Permission>();


        // Administrator can delegate every permission.
        if (user.IsInRole(AdministratorRole))
        {
            return await _db.Permissions
                .OrderBy(x => x.Name)
                .ToListAsync();
        }


        var managerRoleIds =
            await GetCurrentUserRoleIdsAsync(user);

        if (!managerRoleIds.Any())
            return new List<Permission>();


        return await _db.RolePermissionManagements
            .Where(x =>
                managerRoleIds.Contains(
                    x.ManagerRoleId)
                &&
                x.TargetRoleId == targetRoleId)
            .Select(x => x.Permission)
            .Distinct()
            .OrderBy(x => x.Name)
            .ToListAsync();
    }


    /// <summary>
    /// Gets the permission IDs currently assigned to a role.
    /// The caller must be authorized to manage the role.
    /// </summary>
    public async Task<HashSet<int>>
        GetRolePermissionIdsAsync(
            ClaimsPrincipal user,
            string targetRoleId)
    {
        if (user.Identity?.IsAuthenticated != true)
            return new HashSet<int>();

        if (string.IsNullOrWhiteSpace(targetRoleId))
            return new HashSet<int>();


        if (!await CanManageRoleAsync(
                user,
                targetRoleId))
        {
            return new HashSet<int>();
        }


        var permissionIds =
            await _db.RolePermissions
                .Where(x =>
                    x.RoleId == targetRoleId)
                .Select(x => x.PermissionId)
                .ToListAsync();


        return permissionIds.ToHashSet();
    }


    /// <summary>
    /// Saves the permissions assigned to a target role.
    ///
    /// The requested permission IDs are NEVER trusted.
    /// The service calculates which permissions the current
    /// user is actually allowed to delegate and only changes
    /// those permissions.
    /// </summary>
    public async Task<bool> SaveRolePermissionsAsync(
        ClaimsPrincipal user,
        string targetRoleId,
        IEnumerable<int> requestedPermissionIds)
    {
        if (user.Identity?.IsAuthenticated != true)
            return false;

        if (string.IsNullOrWhiteSpace(targetRoleId))
            return false;


        // First verify that the user can manage the role.
        if (!await CanManageRoleAsync(
                user,
                targetRoleId))
        {
            return false;
        }


        var requestedIds =
            requestedPermissionIds
                .Distinct()
                .ToHashSet();


        // Determine which permissions this user is allowed
        // to delegate to this target role.
        var manageablePermissions =
            await GetDelegatablePermissionsAsync(
                user,
                targetRoleId);


        var manageablePermissionIds =
            manageablePermissions
                .Select(x => x.Id)
                .ToHashSet();


        // Ignore any permission supplied by the client
        // that the current user is not authorized to manage.
        var authorizedRequestedIds =
            requestedIds
                .Where(x =>
                    manageablePermissionIds.Contains(x))
                .ToHashSet();


        var existing =
            await _db.RolePermissions
                .Where(x =>
                    x.RoleId == targetRoleId)
                .ToListAsync();


        var existingPermissionIds =
            existing
                .Select(x => x.PermissionId)
                .ToHashSet();


        // Remove only permissions that the current manager
        // has authority over.
        var permissionsToRemove =
            existing
                .Where(x =>
                    manageablePermissionIds.Contains(
                        x.PermissionId)
                    &&
                    !authorizedRequestedIds.Contains(
                        x.PermissionId))
                .ToList();


        _db.RolePermissions.RemoveRange(
            permissionsToRemove);


        // Add only permissions that the current manager
        // is authorized to delegate.
        foreach (var permissionId
            in authorizedRequestedIds)
        {
            if (existingPermissionIds.Contains(
                    permissionId))
            {
                continue;
            }


            _db.RolePermissions.Add(
                new RolePermission
                {
                    RoleId = targetRoleId,
                    PermissionId = permissionId
                });
        }


        await _db.SaveChangesAsync();

        return true;
    }


    // ============================================================
    // GENERAL PERMISSION CHECKING
    // ============================================================

    /// <summary>
    /// Determines whether the current user has a specific
    /// permission through any of their roles.
    ///
    /// Administrator automatically has every permission.
    /// </summary>
    public async Task<bool> HasPermissionAsync(
    ClaimsPrincipal user,
    string permissionName)
    {
        if (user.Identity?.IsAuthenticated != true)
            return false;

        if (string.IsNullOrWhiteSpace(permissionName))
            return false;


        // Administrator has unrestricted access.
        if (user.IsInRole(AdministratorRole))
            return true;


        var roleIds =
            await GetCurrentUserRoleIdsAsync(user);

        if (!roleIds.Any())
            return false;


        return await _db.RolePermissions
            .AnyAsync(rp =>
                roleIds.Contains(rp.RoleId)
                &&
                rp.Permission.Name == permissionName);
    }


    // ============================================================
    // INTERNAL HELPERS
    // ============================================================

    /// <summary>
    /// Gets the IdentityRole IDs for the current user.
    /// </summary>
    private async Task<List<string>>
        GetCurrentUserRoleIdsAsync(
            ClaimsPrincipal user)
    {
        var currentUser =
            await _userManager.GetUserAsync(user);

        if (currentUser == null)
            return new List<string>();


        var roleNames =
            await _userManager.GetRolesAsync(
                currentUser);

        if (!roleNames.Any())
            return new List<string>();


        return await _roleManager.Roles
            .Where(x =>
                x.Name != null &&
                roleNames.Contains(x.Name))
            .Select(x => x.Id)
            .ToListAsync();
    }


    /// <summary>
    /// Gets the current ApplicationUser or throws if the
    /// authenticated principal no longer maps to a user.
    /// </summary>
    private async Task<ApplicationUser>
        GetRequiredUserAsync(
            ClaimsPrincipal user)
    {
        var currentUser =
            await _userManager.GetUserAsync(user);

        if (currentUser == null)
        {
            throw new InvalidOperationException(
                "The authenticated user could not be found.");
        }

        return currentUser;
    }
}