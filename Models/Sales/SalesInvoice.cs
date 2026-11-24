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
    // Null is reserved for historical invoices whose type was never recorded.
    public SalesInvoiceType? InvoiceType { get; set; }
    public string? BuyerProfileJson { get; set; }
    public string? CurrencyCode { get; set; }
    public string? SellerProfileJson { get; set; }
    public decimal? VatAmount { get; set; }
    public string? TaxReportJson { get; set; }
    public string? TaxReportStatus { get; set; }
    public string? TaxAuthorityReference { get; set; }
    public string? TaxReportAuditJson { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TotalAmount { get; set; }
    // Full payment confirmation by an authorized employee, not an online payment processor.
    public DateTime? PaidAtUtc { get; set; }
    public string? PaidByUserId { get; set; }
    public string? PaymentReference { get; set; }
    public string Notes { get; set; } = "";
    public string? CreatedByUserId { get; set; }

    public ICollection<SalesInvoiceLine> Lines { get; set; } = new List<SalesInvoiceLine>();
}
