using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Sales;

public class ApprovalHistory : BaseEntity
{
    public long Id { get; set; }



    public long QuotationId { get; set; }

    public Quotation Quotation { get; set; }
        = null!;



    public string UserId { get; set; } = "";

    public ApplicationUser User { get; set; }
        = null!;



    public bool Approved { get; set; }



    public string Comment { get; set; } = "";
}