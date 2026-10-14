using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Production;

public class ProductionSchedule : BaseEntity
{
    public long Id { get; set; }



    public long ProductionOperationId { get; set; }


    public ProductionOperation ProductionOperation { get; set; }
        = null!;



    public int MachineId { get; set; }


    public Machine Machine { get; set; }
        = null!;



    public DateTime PlannedStart { get; set; }



    public DateTime PlannedEnd { get; set; }



    public int Priority { get; set; }
        = 1;



    public ScheduleStatus Status { get; set; }
        = ScheduleStatus.Planned;
}