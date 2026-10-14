using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Production;

public class BillOfMaterialLine : BaseEntity
{
    public long Id { get; set; }



    public long BillOfMaterialId { get; set; }

    public BillOfMaterial BillOfMaterial { get; set; }
        = null!;



    // Raw material
    public int ItemId { get; set; }

    public Item Item { get; set; }
        = null!;



    // Quantity required per 1 finished product
    public decimal Quantity { get; set; }



    public int UnitId { get; set; }

    public Unit Unit { get; set; } = null!;



    public string Notes { get; set; } = "";
}