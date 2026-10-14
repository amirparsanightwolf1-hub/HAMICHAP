using HamiChap_MES_ERP.Models.Customers;
using HamiChap_MES_ERP.Models.Sales.Enums;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Sales;

public class Quotation : BaseEntity
{
    public long Id { get; set; }


    // QT-2026-000001
    public string QuotationNumber { get; set; } = "";


    public long SalesRequestId { get; set; }

    public SalesRequest SalesRequest { get; set; } = null!;



    public int CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;



    public QuotationStatus Status { get; set; }
        = QuotationStatus.Draft;



    public DateTime QuotationDate { get; set; }
        = DateTime.Now;



    public DateTime? ValidUntil { get; set; }



    public decimal MaterialCost { get; set; }


    public decimal ProductionCost { get; set; }


    public decimal ProfitMargin { get; set; }



    public decimal FinalAmount { get; set; }



    public int EstimatedDeliveryDays { get; set; }



    public string Notes { get; set; } = "";



    public ICollection<QuotationLine> Lines { get; set; }
        = new List<QuotationLine>();
}