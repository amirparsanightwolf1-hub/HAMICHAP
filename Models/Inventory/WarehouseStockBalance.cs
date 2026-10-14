namespace HamiChap_MES_ERP.Models.Inventory;

public class WarehouseStockBalance
{
    public int ItemId { get; set; }

    public string ItemCode { get; set; } = "";

    public string ItemName { get; set; } = "";

    public int? LocationId { get; set; }

    public string LocationName { get; set; } = "";

    public decimal OnHand { get; set; }

    public decimal Reserved { get; set; }

    public decimal Available => OnHand - Reserved;
}