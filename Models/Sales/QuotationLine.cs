using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Sales;

public class QuotationLine : BaseEntity
{
    public long Id { get; set; }



    public long QuotationId { get; set; }

    public Quotation Quotation { get; set; }
        = null!;

    // The originating Roll/Sheet request (and its independent approval proof).
    // Nullable for historical quotations until the data migration is applied.
    public long? SourceRequestId { get; set; }
    public SalesRequest? SourceRequest { get; set; }



    public int ItemId { get; set; }

    public Item Item { get; set; }
        = null!;



    public decimal Quantity { get; set; }



    public decimal UnitPrice { get; set; }



    public decimal TotalPrice =>
        Quantity * UnitPrice;



    public string Notes { get; set; } = "";
}