using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Production;

public class MachineCalendar : BaseEntity
{
    public long Id { get; set; }


    public int MachineId { get; set; }


    public Machine Machine { get; set; }
        = null!;



    public DayOfWeek DayOfWeek { get; set; }



    public TimeSpan StartTime { get; set; }


    public TimeSpan EndTime { get; set; }



    public bool IsWorkingDay { get; set; } = true;
}