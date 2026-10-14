using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Production;

public class MaterialRequirementPlanLine : BaseEntity
{
    public long Id { get; set; }



    public long MaterialRequirementPlanId { get; set; }

    public MaterialRequirementPlan Plan { get; set; }
        = null!;



    public int ItemId { get; set; }

    public Item Item { get; set; }
        = null!;



    public decimal RequiredQuantity { get; set; }



    public decimal AvailableQuantity { get; set; }



    public decimal ShortageQuantity =>
        RequiredQuantity - AvailableQuantity;



    public bool IsShortage =>
        ShortageQuantity > 0;



    public int UnitId { get; set; }

    public Unit Unit { get; set; }
        = null!;
}