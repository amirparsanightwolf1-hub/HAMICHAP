using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Production;

public class ProductionMaterialReservation : BaseEntity
{
    public long Id { get; set; }

    public long ProductionOrderId { get; set; }

    public ProductionOrder ProductionOrder { get; set; }
        = null!;

    public int ItemId { get; set; }

    public Item Item { get; set; }
        = null!;

    public decimal Quantity { get; set; }

    public int UnitId { get; set; }

    public Unit Unit { get; set; }
        = null!;

    public int InventoryLocationId { get; set; }

    public InventoryLocation InventoryLocation { get; set; }
        = null!;

    public long? InventoryLotId { get; set; }

    public InventoryLot? InventoryLot { get; set; }

    public DateTime ReservedDate { get; set; }
        = DateTime.Now;

    public ReservationStatus Status { get; set; }
        = ReservationStatus.Reserved;
}