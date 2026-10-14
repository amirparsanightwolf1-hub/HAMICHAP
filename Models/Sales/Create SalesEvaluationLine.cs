using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Sales;

public class SalesEvaluationLine : BaseEntity
{
    public long Id { get; set; }



    public long SalesEvaluationId { get; set; }

    public SalesEvaluation SalesEvaluation { get; set; }
        = null!;



    public int ItemId { get; set; }

    public Item Item { get; set; }
        = null!;



    public decimal RequiredQuantity { get; set; }



    public decimal AvailableQuantity { get; set; }



    public decimal ShortageQuantity =>
        RequiredQuantity - AvailableQuantity;



    public bool IsAvailable =>
        ShortageQuantity <= 0;



    public string Notes { get; set; } = "";
}