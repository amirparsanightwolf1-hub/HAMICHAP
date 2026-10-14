using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Inventory;

public class InventoryLot : BaseEntity
{
    public long Id { get; set; }

    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    // Supplier or customer batch number
    public string LotNumber { get; set; } = "";

    // Optional serial number (for individual rolls, pallets, etc.)
    public string? SerialNumber { get; set; }

    public DateTime ReceivedDate { get; set; } = DateTime.Now;

    public DateTime? ExpirationDate { get; set; }

    public decimal OriginalQuantity { get; set; }

    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public bool IsClosed { get; set; }
}