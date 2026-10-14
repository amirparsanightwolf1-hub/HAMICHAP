namespace HamiChap_MES_ERP.Models.Administration;

public class RolePermission
{
    public int Id { get; set; }

    public string RoleId { get; set; } = "";

    public int PermissionId { get; set; }


    public Permission Permission { get; set; } = null!;
}