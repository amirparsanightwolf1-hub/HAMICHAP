using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Sales;

public class SalesRequestLine : BaseEntity
{
    public long Id { get; set; }


    public long SalesRequestId { get; set; }

    public SalesRequest SalesRequest { get; set; } = null!;


    // Requested item
    public int ItemId { get; set; }

    public Item Item { get; set; } = null!;


    // Requested quantity
    public decimal Quantity { get; set; }


    public int UnitId { get; set; }

    public Unit Unit { get; set; } = null!;


    public string Notes { get; set; } = "";
}