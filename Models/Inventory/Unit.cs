using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Inventory;

public class Unit : BaseEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Symbol { get; set; } = "";

    public bool IsActive { get; set; } = true;
}