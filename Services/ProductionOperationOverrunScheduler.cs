using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Production;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

/// <summary>
/// Extends a running machine operation and moves its remaining order stages to the next
/// feasible free machine intervals. Never changes released, running or completed downstream bookings.
/// Must be called within the caller's serializable transaction.
/// </summary>
public static class ProductionOperationOverrunScheduler
{
    public static async Task ExtendAsync(
        ApplicationDbContext db, ProductionOperation operation,
        ProductionSchedule runningBooking, DateTime requestedEnd)
    {
        if (runningBooking.Status != ScheduleStatus.Running ||
            operation.Status != ProductionOperationStatus.Running)
            throw new InvalidOperationException("Only running operations can be extended.");

        var order = operation.ProductionOrder;
        var end = ProductionPlanningTime.NextAllowedProductionMinute(
            requestedEnd, order.AvoidOvertime, order.AvoidNightShift);
        if (end <= runningBooking.PlannedEnd)
            return;

        var later = await db.ProductionOperations
            .Where(x => x.ProductionOrderId == operation.ProductionOrderId &&
                        x.Sequence > operation.Sequence)
            .OrderBy(x => x.Sequence).ThenBy(x => x.Id).ToListAsync();
        var laterIds = later.Select(x => x.Id).ToList();
        var bookings = await db.ProductionSchedules
            .Where(x => laterIds.Contains(x.ProductionOperationId) &&
                        x.Status != ScheduleStatus.Cancelled)
            .ToListAsync();

        if (later.Any(x => x.Status is ProductionOperationStatus.Running or
                              ProductionOperationStatus.Completed) ||
            bookings.Any(x => x.Status != ScheduleStatus.Planned))
            throw new InvalidOperationException(
                "A downstream operation is already active or completed. A manager must resolve its schedule.");

        var conflictingActive = await db.ProductionSchedules
            .Where(x => x.Id != runningBooking.Id &&
                        x.MachineId == runningBooking.MachineId &&
                        x.Status != ScheduleStatus.Cancelled &&
                        x.Status != ScheduleStatus.Planned &&
                        x.PlannedStart < end && x.PlannedEnd > runningBooking.PlannedEnd)
            .AnyAsync();
        if (conflictingActive)
            throw new InvalidOperationException("An active machine booking prevents this extension.");

        // Planned jobs belonging to other orders retain their reservations; later stages
        // of this order are moved past those reservations rather than double-booked.
        var busy = await db.ProductionSchedules.AsNoTracking()
            .Where(x => x.Status != ScheduleStatus.Cancelled &&
                        x.Id != runningBooking.Id &&
                        !laterIds.Contains(x.ProductionOperationId))
            .Select(x => new { x.MachineId, x.PlannedStart, x.PlannedEnd })
            .ToListAsync();
        var reserved = busy.Select(x =>
            (x.MachineId, x.PlannedStart, x.PlannedEnd)).ToList();

        // A running job can overrun into another PLANNED booking on its own machine.
        // Move that planned booking only if it belongs to this order; for other orders,
        // reject rather than silently mutate another manager's approved route.
        if (reserved.Any(x => x.MachineId == runningBooking.MachineId &&
            x.PlannedStart < end && x.PlannedEnd > runningBooking.PlannedEnd))
            throw new InvalidOperationException(
                "The extension overlaps another order's machine reservation. Ask the production manager to replan it.");

        var placements = new List<(ProductionOperation Step, ProductionSchedule? Booking,
            DateTime Start, DateTime End)>();
        var cursor = end;
        foreach (var step in later)
        {
            var booking = bookings.SingleOrDefault(x => x.ProductionOperationId == step.Id);
            if (!step.IsOutsourced && booking == null)
                throw new InvalidOperationException("A downstream machine operation has no booking.");

            var originalStart = step.IsOutsourced ? step.OutsourcedPlannedStart : booking?.PlannedStart;
            if (originalStart == null)
                throw new InvalidOperationException("A downstream operation has no planned start.");

            var candidate = ProductionPlanningTime.NextAllowedProductionMinute(
                originalStart.Value > cursor ? originalStart.Value : cursor,
                order.AvoidOvertime, order.AvoidNightShift);
            DateTime candidateEnd = candidate;
            bool fitted = false;
            for (var attempt = 0; attempt < 10000; attempt++)
            {
                candidateEnd = ProductionPlanningTime.CalculateEndDateTime(candidate,
                    ProductionPlanningTime.OperationMinutes(step.PlannedHours),
                    order.AvoidOvertime, order.AvoidNightShift);
                var conflict = booking == null ? null : reserved
                    .Where(x => x.MachineId == booking.MachineId &&
                                x.PlannedStart < candidateEnd && candidate < x.PlannedEnd)
                    .OrderBy(x => x.PlannedEnd).FirstOrDefault();
                if (booking == null || conflict == default)
                {
                    fitted = true;
                    break;
                }
                candidate = ProductionPlanningTime.NextAllowedProductionMinute(
                    conflict.PlannedEnd > candidate ? conflict.PlannedEnd : candidate.AddMinutes(1),
                    order.AvoidOvertime, order.AvoidNightShift);
            }
            if (!fitted)
                throw new InvalidOperationException("Cannot find a free slot for the downstream operation.");

            placements.Add((step, booking, candidate, candidateEnd));
            if (booking != null)
                reserved.Add((booking.MachineId, candidate, candidateEnd));
            cursor = candidateEnd;
        }

        // Commit the whole replan only when all stages have feasible placements.
        runningBooking.PlannedEnd = end;
        foreach (var placement in placements)
        {
            if (placement.Booking == null)
            {
                placement.Step.OutsourcedPlannedStart = placement.Start;
                placement.Step.OutsourcedPlannedEnd = placement.End;
            }
            else
            {
                placement.Booking.PlannedStart = placement.Start;
                placement.Booking.PlannedEnd = placement.End;
            }
        }
    }
}
