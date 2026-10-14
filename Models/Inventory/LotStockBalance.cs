namespace HamiChap_MES_ERP.Models.Inventory;

public class LotStockBalance
{
    public int ItemId { get; set; }

    public string ItemCode { get; set; } = "";

    public string ItemName { get; set; } = "";

    public long InventoryLotId { get; set; }

    public string LotNumber { get; set; } = "";

    public DateTime? ExpirationDate { get; set; }

    public int? LocationId { get; set; }

    public string LocationName { get; set; } = "";

    public decimal Quantity { get; set; }
}