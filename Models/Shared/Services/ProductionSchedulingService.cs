using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Production;
using Microsoft.EntityFrameworkCore;


namespace HamiChap_MES_ERP.Services;


public class ProductionSchedulingService
{
    private readonly ApplicationDbContext _db;


    public ProductionSchedulingService(
        ApplicationDbContext db)
    {
        _db = db;
    }



    public async Task<ProductionSchedule>
        ScheduleOperationAsync(
            long operationId,
            int machineId,
            decimal estimatedHours)
    {

        var lastSchedule =
            await _db.ProductionSchedules
            .Where(x =>
                x.MachineId == machineId &&
                x.Status != ScheduleStatus.Cancelled)
            .OrderByDescending(x =>
                x.PlannedEnd)
            .FirstOrDefaultAsync();



        var start =
            lastSchedule?.PlannedEnd
            ?? DateTime.Now;



        var end =
            start.AddHours(
                (double)estimatedHours);



        var schedule =
            new ProductionSchedule
            {
                ProductionOperationId =
                    operationId,

                MachineId =
                    machineId,

                PlannedStart =
                    start,

                PlannedEnd =
                    end,

                Status =
                    ScheduleStatus.Planned
            };



        _db.ProductionSchedules.Add(schedule);


        await _db.SaveChangesAsync();


        return schedule;
    }
}