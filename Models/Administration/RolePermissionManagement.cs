namespace HamiChap_MES_ERP.Models.Administration;

public class RolePermissionManagement
{
    public long Id { get; set; }

    // Role that is allowed to manage the permission
    public string ManagerRoleId { get; set; } = "";

    // Role whose permission can be managed
    public string TargetRoleId { get; set; } = "";

    // Permission that the manager is allowed to grant/remove
    public int PermissionId { get; set; }

    public Permission Permission { get; set; } = null!;
}