using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Sales;

public class SalesDeliveryLine : BaseEntity
{
    public long Id { get; set; }

    public long SalesDeliveryId { get; set; }

    public SalesDelivery SalesDelivery { get; set; } = null!;

    public long SalesOrderLineId { get; set; }

    public SalesOrderLine SalesOrderLine { get; set; } = null!;

    public int ItemId { get; set; }

    public Item Item { get; set; } = null!;

    public decimal Quantity { get; set; }

    public int UnitId { get; set; }

    public Unit Unit { get; set; } = null!;

    public long? InventoryDocumentId { get; set; }

    public InventoryDocument? InventoryDocument { get; set; }

    public ICollection<SalesDeliveryAllocation> Allocations { get; set; }
    = new List<SalesDeliveryAllocation>();

    public string Notes { get; set; } = "";
}