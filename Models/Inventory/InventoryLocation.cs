using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Inventory;

public class InventoryLocation : BaseEntity
{
    public int Id { get; set; }

    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    public InventoryLocationType Type { get; set; }

    public int? CustomerId { get; set; }

    public int? MachineId { get; set; }

    public bool IsActive { get; set; } = true;
}