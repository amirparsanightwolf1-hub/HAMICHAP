namespace HamiChap_MES_ERP.Models.Inventory;

public class LowStockAlert
{
    public int ItemId { get; set; }

    public string ItemCode { get; set; } = "";

    public string ItemName { get; set; } = "";

    public string UnitSymbol { get; set; } = "";

    public decimal AvailableQuantity { get; set; }

    public decimal Threshold { get; set; }

    public decimal ShortageQuantity =>
        Threshold - AvailableQuantity;
}