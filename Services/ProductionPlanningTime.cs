namespace HamiChap_MES_ERP.Services;

/// <summary>
/// Shared CEO / machine planner calculations. All durations are productive minutes:
/// 13:00-14:00 and excluded shifts do not contribute to the planned time.
/// </summary>
public static class ProductionPlanningTime
{
    // Counts only production minutes. The lunch break and any shifts prohibited for this order are skipped.
    public static DateTime CalculateEndDateTime(DateTime start, int durationMinutes, bool avoidOvertime, bool avoidNight)
    {
        var cursor = start;
        var remaining = durationMinutes;
        var guard = 0;

        while (remaining > 0 && guard++ < 20000)
        {
            var next = NextAllowedProductionMinute(cursor, avoidOvertime, avoidNight);
            var availableUntil = EndOfCurrentAllowedWindow(next, avoidOvertime, avoidNight);
            var availableMinutes = (int)Math.Floor((availableUntil - next).TotalMinutes);

            if (availableMinutes <= 0)
            {
                cursor = next.AddMinutes(1);
                continue;
            }

            var used = Math.Min(remaining, availableMinutes);
            cursor = next.AddMinutes(used);
            remaining -= used;
        }

        if (remaining > 0)
            throw new InvalidOperationException("Unable to calculate an end time within the scheduling horizon.");

        return cursor;
    }

    public static DateTime NextAllowedProductionMinute(DateTime value, bool avoidOvertime, bool avoidNight)
    {
        var cursor = value;
        for (var i = 0; i < 20000; i++)
        {
            var t = cursor.TimeOfDay;

            if (t >= TimeSpan.FromHours(8) && t < TimeSpan.FromHours(13))
                return cursor;

            if (t >= TimeSpan.FromHours(14) && t < TimeSpan.FromHours(17))
                return cursor;

            if (!avoidOvertime && t >= TimeSpan.FromHours(17) && t < TimeSpan.FromHours(22))
                return cursor;

            if (!avoidNight && (t >= TimeSpan.FromHours(22) || t < TimeSpan.FromHours(8)))
                return cursor;

            if (t >= TimeSpan.FromHours(13) && t < TimeSpan.FromHours(14))
                cursor = cursor.Date.AddHours(14);
            else if (avoidOvertime && t >= TimeSpan.FromHours(17) && t < TimeSpan.FromHours(22))
                cursor = avoidNight ? cursor.Date.AddDays(1).AddHours(8) : cursor.Date.AddHours(22);
            else if (avoidNight && (t >= TimeSpan.FromHours(22) || t < TimeSpan.FromHours(8)))
                cursor = t >= TimeSpan.FromHours(22)
                    ? cursor.Date.AddDays(1).AddHours(8)
                    : cursor.Date.AddHours(8);
            else
                cursor = cursor.AddMinutes(1);
        }

        throw new InvalidOperationException("No permitted production time was found.");
    }

    private static DateTime EndOfCurrentAllowedWindow(DateTime value, bool avoidOvertime, bool avoidNight)
    {
        var t = value.TimeOfDay;
        if (t >= TimeSpan.FromHours(8) && t < TimeSpan.FromHours(13))
            return value.Date.AddHours(13);
        if (t >= TimeSpan.FromHours(14) && t < TimeSpan.FromHours(17))
            return value.Date.AddHours(17);
        if (!avoidOvertime && t >= TimeSpan.FromHours(17) && t < TimeSpan.FromHours(22))
            return value.Date.AddHours(22);
        if (!avoidNight && t >= TimeSpan.FromHours(22))
            return value.Date.AddDays(1).AddHours(8);
        if (!avoidNight && t < TimeSpan.FromHours(8))
            return value.Date.AddHours(8);
        return value.AddMinutes(1);
    }


    public static int OperationMinutes(decimal plannedHours) =>
        Math.Max(1, (int)Math.Round(plannedHours * 60m, MidpointRounding.AwayFromZero));

    /// <summary>Allocate every minute of the CEO's total, preserving relative operation weights.</summary>
    public static int[] AllocateOperationMinutes(int totalMinutes, IReadOnlyList<decimal> weights)
    {
        if (weights.Count == 0) return Array.Empty<int>();
        if (totalMinutes < weights.Count)
            throw new InvalidOperationException("The CEO duration must allow at least one minute per operation.");

        var normalized = weights.Select(w => Math.Max(0.01m, w)).ToArray();
        var sum = normalized.Sum();
        var distributable = totalMinutes - normalized.Length;
        var portions = normalized.Select(w => distributable * w / sum).ToArray();
        var minutes = portions.Select(x => 1 + (int)decimal.Truncate(x)).ToArray();
        var remaining = totalMinutes - minutes.Sum();

        foreach (var index in Enumerable.Range(0, weights.Count)
                     .OrderByDescending(i => portions[i] - decimal.Truncate(portions[i]))
                     .ThenBy(i => i).Take(remaining))
            minutes[index]++;

        return minutes;
    }
}
