using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Production;

public class BillOfMaterial : BaseEntity
{
    public long Id { get; set; }


    // Finished product
    public int ItemId { get; set; }

    public Item Item { get; set; } = null!;


    public string Version { get; set; } = "1.0";


    public bool IsActive { get; set; } = true;



    public ICollection<BillOfMaterialLine> Lines { get; set; }
        = new List<BillOfMaterialLine>();
}