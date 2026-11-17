using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Production;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

/// <summary>
/// Propagates a running operation's delay through order precedence and shared-machine
/// reservations. Only PLANNED machine bookings and not-yet-started external stages may move.
/// Caller must hold a serializable database transaction and save the tracked entities.
/// </summary>
public static class ProductionOperationOverrunScheduler
{
    private sealed class Slot
    {
        public required ProductionOperation Operation { get; init; }
        public ProductionSchedule? Booking { get; init; }
        public int? MachineId => Booking?.MachineId;
        public required DateTime OriginalStart { get; init; }
        public required DateTime OriginalEnd { get; init; }
        public required int ProductiveMinutes { get; init; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public bool Movable => Booking != null
            ? Booking.Status == ScheduleStatus.Planned
            : Operation.Status is ProductionOperationStatus.Pending or ProductionOperationStatus.Ready;
    }

    public static async Task ExtendAsync(
        ApplicationDbContext db, ProductionOperation operation,
        ProductionSchedule runningBooking, DateTime requestedEnd)
    {
        if (operation.Status != ProductionOperationStatus.Running ||
            runningBooking.Status != ScheduleStatus.Running)
            throw new InvalidOperationException("Only running machine operations can be extended.");
        if (runningBooking.ProductionOperationId != operation.Id)
            throw new InvalidOperationException("Booking and operation do not match.");

        var order = operation.ProductionOrder;
        var extendedEnd = ProductionPlanningTime.NextAllowedProductionMinute(
            requestedEnd, order.AvoidOvertime, order.AvoidNightShift);
        if (extendedEnd <= runningBooking.PlannedEnd) return;

        var schedules = await db.ProductionSchedules
            .Include(x => x.ProductionOperation).ThenInclude(x => x.ProductionOrder)
            .Where(x => x.Status != ScheduleStatus.Cancelled).ToListAsync();

        // Outsourced steps have no machine booking but still block subsequent steps.
        var outsourced = await db.ProductionOperations
            .Include(x => x.ProductionOrder)
            .Where(x => x.IsOutsourced &&
                        x.OutsourcedPlannedStart.HasValue &&
                        x.OutsourcedPlannedEnd.HasValue &&
                        x.Status != ProductionOperationStatus.Cancelled).ToListAsync();

        var slots = new List<Slot>();
        foreach (var schedule in schedules)
        {
            var associated = schedule.ProductionOperation;
            var scheduledOrder = associated.ProductionOrder;
            var minutes = (int)Math.Round(ProductionPlanningTime.ProductiveSegments(
                    schedule.PlannedStart, schedule.PlannedEnd,
                    scheduledOrder.AvoidOvertime, scheduledOrder.AvoidNightShift)
                .Sum(x => (x.End - x.Start).TotalMinutes), MidpointRounding.AwayFromZero);
            slots.Add(new Slot
            {
                Operation = associated, Booking = schedule,
                OriginalStart = schedule.PlannedStart, OriginalEnd = schedule.PlannedEnd,
                Start = schedule.PlannedStart, End = schedule.PlannedEnd,
                ProductiveMinutes = Math.Max(1, minutes)
            });
        }
        foreach (var outsourcedStep in outsourced)
        {
            var start = outsourcedStep.OutsourcedPlannedStart!.Value;
            var end = outsourcedStep.OutsourcedPlannedEnd!.Value;
            slots.Add(new Slot
            {
                Operation = outsourcedStep,
                OriginalStart = start, OriginalEnd = end, Start = start, End = end,
                ProductiveMinutes = ProductionPlanningTime.OperationMinutes(outsourcedStep.PlannedHours)
            });
        }

        var anchor = slots.SingleOrDefault(x => x.Booking?.Id == runningBooking.Id);
        if (anchor == null)
            throw new InvalidOperationException("Running machine booking was not found.");
        anchor.End = extendedEnd;

        // Freeze the existing machine order; rescheduling can delay future bookings,
        // but cannot put a later booking ahead of an earlier one.
        var machinePrevious = new Dictionary<Slot, Slot>();
        foreach (var machineGroup in slots.Where(x => x.MachineId.HasValue)
                     .GroupBy(x => x.MachineId!.Value))
        {
            Slot? previous = null;
            foreach (var slot in machineGroup.OrderBy(x => x.OriginalStart)
                         .ThenBy(x => x.Booking!.Id))
            {
                if (previous != null) machinePrevious[slot] = previous;
                previous = slot;
            }
        }

        var orderPrevious = new Dictionary<Slot, Slot>();
        foreach (var orderGroup in slots.GroupBy(x => x.Operation.ProductionOrderId))
        {
            Slot? previous = null;
            foreach (var slot in orderGroup.OrderBy(x => x.Operation.Sequence)
                         .ThenBy(x => x.Operation.Id))
            {
                if (previous != null) orderPrevious[slot] = previous;
                previous = slot;
            }
        }

        // Relax both constraints until stable. Each move is forward-only. This
        // propagates delays across machines and into downstream stages of other orders.
        var stabilized = false;
        var maxPasses = Math.Max(8, slots.Count * 2 + 2);
        for (var pass = 0; pass < maxPasses; pass++)
        {
            var changed = false;
            foreach (var slot in slots.OrderBy(x => x.OriginalStart)
                         .ThenBy(x => x.Operation.Id))
            {
                if (ReferenceEquals(slot, anchor)) continue;

                var earliest = slot.OriginalStart;
                if (machinePrevious.TryGetValue(slot, out var machinePredecessor) &&
                    machinePredecessor.End > earliest)
                    earliest = machinePredecessor.End;
                if (orderPrevious.TryGetValue(slot, out var orderPredecessor) &&
                    orderPredecessor.End > earliest)
                    earliest = orderPredecessor.End;

                if (earliest <= slot.Start) continue;
                if (!slot.Movable)
                    throw new InvalidOperationException(
                        "A delayed operation would overlap work that is already running or completed. The production manager must resolve the conflict.");

                var plannedOrder = slot.Operation.ProductionOrder;
                var start = ProductionPlanningTime.NextAllowedProductionMinute(
                    earliest, plannedOrder.AvoidOvertime, plannedOrder.AvoidNightShift);
                var end = ProductionPlanningTime.CalculateEndDateTime(
                    start, slot.ProductiveMinutes,
                    plannedOrder.AvoidOvertime, plannedOrder.AvoidNightShift);
                if (start <= slot.Start) continue;
                slot.Start = start;
                slot.End = end;
                changed = true;
            }
            if (!changed)
            {
                stabilized = true;
                break;
            }
        }
        if (!stabilized)
            throw new InvalidOperationException("Schedule dependencies could not be resolved.");

        // Confirm no unexpected overlap remains before mutating tracked records.
        foreach (var group in slots.Where(x => x.MachineId.HasValue)
                     .GroupBy(x => x.MachineId!.Value))
        {
            var ordered = group.OrderBy(x => x.Start).ThenBy(x => x.Booking!.Id).ToList();
            for (var i = 1; i < ordered.Count; i++)
                if (ordered[i - 1].End > ordered[i].Start)
                    throw new InvalidOperationException("The revised plan contains a machine overlap.");
        }

        foreach (var slot in slots)
        {
            if (slot.Booking != null)
            {
                slot.Booking.PlannedStart = slot.Start;
                slot.Booking.PlannedEnd = slot.End;
            }
            else
            {
                slot.Operation.OutsourcedPlannedStart = slot.Start;
                slot.Operation.OutsourcedPlannedEnd = slot.End;
            }
        }
    }
}
