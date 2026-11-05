using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Production;

public class ProductionMaterialRequestLine : BaseEntity
{
    public long Id { get; set; }
    public long ProductionMaterialRequestId { get; set; }
    public ProductionMaterialRequest Request { get; set; } = null!;
    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public decimal Quantity { get; set; }
    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;
    public string Notes { get; set; } = string.Empty;
}
