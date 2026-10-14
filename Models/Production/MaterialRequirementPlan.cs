using System.Net.NetworkInformation;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Production;

public class MaterialRequirementPlan : BaseEntity
{
    public long Id { get; set; }


    public long ProductionOrderId { get; set; }

    public ProductionOrder ProductionOrder { get; set; }
        = null!;



    public DateTime CreatedDate { get; set; }
        = DateTime.Now;



    public MRPStatus Status { get; set; }
        = MRPStatus.Calculated;



    public ICollection<MaterialRequirementPlanLine> Lines { get; set; }
        = new List<MaterialRequirementPlanLine>();
}