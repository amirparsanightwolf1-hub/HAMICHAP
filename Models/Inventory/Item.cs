using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Inventory;

public class Item : BaseEntity
{
    public int Id { get; set; }

    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    public int CategoryId { get; set; }

    public ItemCategory Category { get; set; } = null!;

    public int UnitId { get; set; }

    public Unit Unit { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public bool IsRawMaterial { get; set; }

    public bool IsFinishedProduct { get; set; }

    public bool RequiresLotTracking { get; set; }

    // =========================================
    // Low Stock Alert
    // =========================================

    public bool EnableLowStockAlert { get; set; }

    public decimal LowStockThreshold { get; set; }
}