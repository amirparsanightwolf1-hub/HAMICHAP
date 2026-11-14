using HamiChap_MES_ERP.Models.Production;

namespace HamiChap_MES_ERP.Services;

/// <summary>
/// Deterministic, sequential, earliest-feasible schedule of one CEO order.
/// Caller holds a serializable DB transaction and persists all results atomically.
/// Production time skips CEO-disallowed shifts and lunch; a busy machine is never
/// assigned a second operation during any intersecting interval.
/// </summary>
public static class ProductionAutomaticScheduler
{
    public sealed record Placement(long OperationId, int MachineId, DateTime Start, DateTime End);
    public sealed record BusyInterval(int MachineId, DateTime Start, DateTime End);

    public static bool TryBuild(
        ProductionOrder order,
        IReadOnlyList<ProductionOperation> steps,
        IReadOnlyList<Machine> machines,
        IReadOnlyCollection<BusyInterval> existing,
        Func<ProductionOperation, Machine, bool> isCandidate,
        out List<Placement> plan,
        out string error)
    {
        plan = new List<Placement>();
        error = "";
        if (order.PlannedStartDate is not DateTime windowStart ||
            order.PlannedEndDate is not DateTime windowEnd || windowEnd <= windowStart)
        {
            error = "The CEO has not assigned a valid production time window.";
            return false;
        }
        if (steps.Count == 0) { error = "This order has no operations."; return false; }

        var occupied = existing.ToList();
        var cursor = windowStart;
        foreach (var step in steps.OrderBy(x => x.Sequence))
        {
            if (step.PlannedHours <= 0)
            {
                error = "Estimated duration is missing for " + step.Name;
                return false;
            }
            var candidates = machines.Where(m => m.IsActive && isCandidate(step, m)).ToList();
            if (candidates.Count == 0)
            {
                error = "Choose a compatible machine for " + step.Name;
                return false;
            }

            Placement? earliest = null;
            foreach (var machine in candidates)
            {
                var candidateCursor = cursor;
                // The cursor strictly advances to the end of a conflicting booking.
                // A guard protects against corrupt/invalid calendar data.
                for (var guard = 0; guard < 10000 && candidateCursor < windowEnd; guard++)
                {
                    var start = ProductionPlanningTime.NextAllowedProductionMinute(
                        candidateCursor, order.AvoidOvertime, order.AvoidNightShift);
                    if (start >= windowEnd) break;
                    var end = ProductionPlanningTime.CalculateEndDateTime(start,
                        ProductionPlanningTime.OperationMinutes(step.PlannedHours),
                        order.AvoidOvertime, order.AvoidNightShift);
                    if (end > windowEnd) break;

                    var conflict = occupied.Where(x => x.MachineId == machine.Id &&
                        x.Start < end && start < x.End).OrderBy(x => x.End).FirstOrDefault();
                    if (conflict == null)
                    {
                        var proposal = new Placement(step.Id, machine.Id, start, end);
                        if (earliest == null || proposal.End < earliest.End ||
                            (proposal.End == earliest.End && proposal.MachineId < earliest.MachineId))
                            earliest = proposal;
                        break;
                    }
                    candidateCursor = conflict.End > candidateCursor
                        ? conflict.End : candidateCursor.AddMinutes(1);
                }
            }

            if (earliest == null)
            {
                error = "The operation '" + step.Name +
                    "' cannot be completed within the CEO window without double-booking a machine.";
                plan.Clear();
                return false;
            }
            plan.Add(earliest);
            occupied.Add(new BusyInterval(earliest.MachineId, earliest.Start, earliest.End));
            cursor = earliest.End;
        }
        return true;
    }
}
