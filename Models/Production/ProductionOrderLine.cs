using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Production;

public class ProductionOrderLine : BaseEntity
{
    public long Id { get; set; }



    public long ProductionOrderId { get; set; }

    public ProductionOrder ProductionOrder { get; set; }
        = null!;



    public int ItemId { get; set; }

    public Item Item { get; set; }
        = null!;



    public decimal PlannedQuantity { get; set; }



    public decimal ProducedQuantity { get; set; }



    public int UnitId { get; set; }

    public Unit Unit { get; set; }
        = null!;
}