using HamiChap_MES_ERP.Models.Customers;
using HamiChap_MES_ERP.Models.Sales.Enums;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Sales;

public class SalesOrder : BaseEntity
{
    public long Id { get; set; }


    // SO-2026-000001
    public string OrderNumber { get; set; } = "";


    public long QuotationId { get; set; }

    public Quotation Quotation { get; set; } = null!;


    public int CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;



    public SalesOrderStatus Status { get; set; }
        = SalesOrderStatus.Confirmed;



    public DateTime OrderDate { get; set; }
        = DateTime.Now;



    public DateTime? DeliveryDate { get; set; }



    public decimal TotalAmount { get; set; }



    public string Notes { get; set; } = "";



    public ICollection<SalesOrderLine> Lines { get; set; }
        = new List<SalesOrderLine>();
}