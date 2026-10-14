using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Sales;

public class SalesDeliveryAllocation : BaseEntity
{
    public long Id { get; set; }


    public long SalesDeliveryLineId { get; set; }

    public SalesDeliveryLine SalesDeliveryLine { get; set; }
        = null!;


    public long InventoryLotId { get; set; }

    public InventoryLot InventoryLot { get; set; }
        = null!;


    public decimal Quantity { get; set; }


    public string Notes { get; set; } = "";
}