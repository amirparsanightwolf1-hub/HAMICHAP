using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Sales;

public class SalesOrderLine : BaseEntity
{
    public long Id { get; set; }


    public long SalesOrderId { get; set; }

    public SalesOrder SalesOrder { get; set; }
        = null!;



    public int ItemId { get; set; }

    public Item Item { get; set; }
        = null!;



    public decimal Quantity { get; set; }



    public int UnitId { get; set; }

    public Unit Unit { get; set; }
        = null!;



    public decimal UnitPrice { get; set; }



    public decimal TotalPrice =>
        Quantity * UnitPrice;

    public decimal DeliveredQuantity { get; set; }

    public decimal ProducedQuantity { get; set; }
}