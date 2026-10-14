using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Production;

public class ProductionOutput : BaseEntity
{
    public long Id { get; set; }



    public long ProductionOrderId { get; set; }

    public ProductionOrder ProductionOrder { get; set; }
        = null!;



    public int ItemId { get; set; }

    public Item Item { get; set; }
        = null!;



    public decimal Quantity { get; set; }



    public decimal WasteQuantity { get; set; }



    public DateTime ProductionDate { get; set; }
        = DateTime.Now;



    public string UserId { get; set; } = "";
}