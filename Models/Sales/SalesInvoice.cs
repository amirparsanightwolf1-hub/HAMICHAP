using HamiChap_MES_ERP.Models.Customers;
using HamiChap_MES_ERP.Models.Sales.Enums;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Sales;

public class SalesInvoice : BaseEntity
{
    public long Id { get; set; }
    public string InvoiceNumber { get; set; } = "";

    public long QuotationId { get; set; }
    public Quotation Quotation { get; set; } = null!;

    public long SalesOrderId { get; set; }
    public SalesOrder SalesOrder { get; set; } = null!;

    public long SalesDeliveryId { get; set; }
    public SalesDelivery SalesDelivery { get; set; } = null!;

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public DateTime InvoiceDate { get; set; } = DateTime.Now;
    public SalesInvoiceStatus Status { get; set; } = SalesInvoiceStatus.Issued;
    public decimal Subtotal { get; set; }
    public decimal TotalAmount { get; set; }
    public string Notes { get; set; } = "";
    public string? CreatedByUserId { get; set; }

    public ICollection<SalesInvoiceLine> Lines { get; set; } = new List<SalesInvoiceLine>();
}
