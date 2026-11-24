using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Production;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

/// <summary>
/// Pulls subsequent PLANNED steps of a production order forward after a predecessor
/// finishes early. Keeps each machine free of conflicting bookings, respects production
/// start and shift restrictions, and never modifies other orders or active steps.
/// Caller owns a serializable transaction and saves the changes.
/// </summary>
public static class ProductionOperationEarlyCompletionScheduler
{
    public static async Task AdvanceFollowingAsync(
        ApplicationDbContext db, ProductionOperation completedOperation, DateTime actualFinish)
    {
        var order = completedOperation.ProductionOrder;
        if (!order.PlannedStartDate.HasValue) return;

        var following = await db.ProductionOperations
            .Where(x => x.ProductionOrderId == order.Id && x.Sequence > completedOperation.Sequence)
            .OrderBy(x => x.Sequence).ThenBy(x => x.Id).ToListAsync();
        if (following.Count == 0) return;

        var ids = following.Select(x => x.Id).ToList();
        var ownBookings = await db.ProductionSchedules
            .Where(x => ids.Contains(x.ProductionOperationId) &&
                        x.Status != ScheduleStatus.Cancelled).ToListAsync();

        // Existing reservations on all machines are obstacles. Each stage is removed
        // from the obstacle list only while finding its own replacement slot.
        var allBookings = await db.ProductionSchedules
            .Where(x => x.Status != ScheduleStatus.Cancelled).ToListAsync();

        var cursor = actualFinish > order.PlannedStartDate.Value
            ? actualFinish : order.PlannedStartDate.Value;

        foreach (var stage in following)
        {
            if (stage.Status is ProductionOperationStatus.Running or
                ProductionOperationStatus.Completed or ProductionOperationStatus.Cancelled)
                break;

            var booking = ownBookings.FirstOrDefault(x => x.ProductionOperationId == stage.Id);
            if (stage.IsOutsourced)
            {
                if (!stage.OutsourcedPlannedStart.HasValue || !stage.OutsourcedPlannedEnd.HasValue)
                    break;

                // External supplier commitments are not shortened automatically.
                // A manager must confirm a new external delivery date.
                var externalEnd = stage.OutsourcedPlannedEnd.Value;
                cursor = externalEnd > cursor ? externalEnd : cursor;
                continue;
            }

            if (booking == null || booking.Status != ScheduleStatus.Planned)
                break;

            var target = ProductionPlanningTime.NextAllowedProductionMinute(
                cursor, order.AvoidOvertime, order.AvoidNightShift);
            var originalStartTime = booking.PlannedStart;
            var originalEndTime = booking.PlannedEnd;
            var length = ProductionPlanningTime.OperationMinutes(stage.PlannedHours);

            DateTime? earlierStart = null;
            DateTime earlierEnd = default;
            for (var attempts = 0; attempts < 1000 && target < originalStartTime; attempts++)
            {
                var end = ProductionPlanningTime.CalculateEndDateTime(
                    target, length, order.AvoidOvertime, order.AvoidNightShift);
                if (end > originalEndTime) break;

                var conflicts = allBookings.Where(x => x.Id != booking.Id &&
                    x.MachineId == booking.MachineId &&
                    x.PlannedStart < end && target < x.PlannedEnd)
                    .OrderBy(x => x.PlannedEnd).ToList();
                if (conflicts.Count == 0)
                {
                    earlierStart = target;
                    earlierEnd = end;
                    break;
                }

                var until = conflicts[0].PlannedEnd;
                target = ProductionPlanningTime.NextAllowedProductionMinute(
                    until > target ? until : target.AddMinutes(1),
                    order.AvoidOvertime, order.AvoidNightShift);
            }

            if (earlierStart.HasValue)
            {
                booking.PlannedStart = earlierStart.Value;
                booking.PlannedEnd = earlierEnd;
            }
            cursor = booking.PlannedEnd > cursor ? booking.PlannedEnd : cursor;
        }
    }
}
