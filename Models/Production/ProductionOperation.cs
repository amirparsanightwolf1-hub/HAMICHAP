using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Production;

public class ProductionOperation : BaseEntity
{
    public long Id { get; set; }



    public long ProductionOrderId { get; set; }

    public ProductionOrder ProductionOrder { get; set; }
        = null!;



    public int Sequence { get; set; }



    public string Name { get; set; } = "";



    public int? WorkCenterId { get; set; }

    public ProductionWorkCenter? WorkCenter { get; set; }



    public decimal PlannedHours { get; set; }



    public decimal ActualHours { get; set; }



    public ProductionOperationStatus Status { get; set; }
        = ProductionOperationStatus.Pending;
}