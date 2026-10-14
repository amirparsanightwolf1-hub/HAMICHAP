using HamiChap_MES_ERP.Models.Sales;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Production;

public class ProductionOrder : BaseEntity
{
    public long Id { get; set; }


    // PO-2026-000001
    public string OrderNumber { get; set; } = "";


    // Source Sales Order
    public long SalesOrderId { get; set; }

    public SalesOrder SalesOrder { get; set; }
        = null!;



    public ProductionOrderStatus Status { get; set; }
        = ProductionOrderStatus.Planned;



    public DateTime CreatedDate { get; set; }
        = DateTime.Now;



    public DateTime? PlannedStartDate { get; set; }


    public DateTime? PlannedEndDate { get; set; }


    public DateTime? ActualStartDate { get; set; }


    public DateTime? ActualEndDate { get; set; }



    public string Notes { get; set; } = "";



    public ICollection<ProductionOrderLine> Lines { get; set; }
        = new List<ProductionOrderLine>();
}