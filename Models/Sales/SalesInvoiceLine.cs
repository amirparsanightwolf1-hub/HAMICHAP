using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Sales;

public class SalesInvoiceLine : BaseEntity
{
    public long Id { get; set; }
    public long SalesInvoiceId { get; set; }
    public SalesInvoice SalesInvoice { get; set; } = null!;

    public long SalesDeliveryLineId { get; set; }
    public SalesDeliveryLine SalesDeliveryLine { get; set; } = null!;

    public long QuotationLineId { get; set; }
    public QuotationLine QuotationLine { get; set; } = null!;

    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string Notes { get; set; } = "";
}
