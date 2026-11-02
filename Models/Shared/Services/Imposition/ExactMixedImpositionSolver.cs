using Google.OrTools.Sat;
using System.Globalization;
using HamiChap_MES_ERP.Models.Imposition;

namespace HamiChap_MES_ERP.Services.Imposition;

/// <summary>
/// Fixed-repeat rectangular mixed-artwork imposition.
/// One solved pattern is repeated identically for every sheet/repeat.
/// Coordinates use 0.1 mm precision.
/// </summary>
internal static class ExactMixedImpositionSolver
{
    private const int UnitsPerMm = 10;

    public static IReadOnlyList<ImpositionAlternative> Solve(MixedImpositionRequest request)
    {
        ValidateRequest(request);

        var usableW = ToUnits(request.StockWidthMm - 2m * request.MarginMm);
        var usableH = ToUnits(request.StockHeightMm - 2m * request.MarginMm);
        var minQuantity = request.Artworks.Min(a => a.Quantity);
        var maxRepeats = minQuantity; // every artwork appears in every identical repeat

        var capacities = request.Artworks.Select(a =>
        {
            var w = ToUnits(a.WidthMm);
            var h = ToUnits(a.HeightMm);
            var best = Math.Max(w <= usableW && h <= usableH ? usableW / w * (usableH / h) : 0,
                a.AllowRotation && h <= usableW && w <= usableH ? usableW / h * (usableH / w) : 0);
            return Math.Max(0, best);
        }).ToArray();

        var lowerBound = 1;
        for (var i = 0; i < request.Artworks.Count; i++)
        {
            if (capacities[i] < 1)
                throw new ArgumentException($"Artwork '{request.Artworks[i].Name}' cannot fit inside the usable stock dimensions.");
            var minimum = Math.Max(1, request.Artworks[i].MinimumPerRepeat);
            if (minimum > capacities[i])
                throw new ArgumentException($"Minimum per repeat for '{request.Artworks[i].Name}' exceeds its individual fit capacity.");
            lowerBound = Math.Max(lowerBound,
                (int)Math.Ceiling((double)request.Artworks[i].Quantity / Math.Max(1, capacities[i])));
        }

        if (lowerBound > maxRepeats)
            throw new ArgumentException("No fixed-repeat plan can satisfy all quantities while including every artwork in each repeat.");

        // Explore several nearby repeat counts to return genuinely different fixed patterns.
        var repeatCandidates = Enumerable.Range(lowerBound, Math.Min(6, maxRepeats - lowerBound + 1)).ToList();
        if (!repeatCandidates.Contains(maxRepeats)) repeatCandidates.Add(maxRepeats);

        var totalSeconds = Math.Clamp(request.SolverTimeLimitSeconds, 1, 300);
        var perCandidateSeconds = Math.Max(0.25, (double)totalSeconds / repeatCandidates.Count);
        var results = new List<ImpositionAlternative>();

        foreach (var repeats in repeatCandidates)
        {
            var solved = SolveOne(request, usableW, usableH, repeats, perCandidateSeconds);
            if (solved.Status is CpSolverStatus.Optimal or CpSolverStatus.Feasible)
                results.Add(BuildResult(request, solved, repeats));
        }

        if (results.Count == 0)
            throw new InvalidOperationException("No valid rectangular fixed-repeat layout was found within the solver time limit.");

        return results
            .OrderBy(x => x.StockAreaMm2)
            .ThenBy(x => x.WastePercent)
            .ThenBy(x => x.PlannedQuantityPerArtwork!.Values.Sum())
            .Take(6)
            .Select((x, i) => x with { Name = $"Rectangular fixed-repeat option {i + 1}" })
            .ToList();
    }

    private static ModelResult SolveOne(MixedImpositionRequest request, int usableW, int usableH,
        int repeats, double seconds)
    {
        var model = new CpModel();
        var xIntervals = new List<IntervalVar>();
        var yIntervals = new List<IntervalVar>();
        var pieces = new List<PieceVars>();
        var gap = ToUnits(request.GapMm);

        for (var ai = 0; ai < request.Artworks.Count; ai++)
        {
            var a = request.Artworks[ai];
            var w0 = ToUnits(a.WidthMm);
            var h0 = ToUnits(a.HeightMm);
            var copies = Math.Max(Math.Max(1, a.MinimumPerRepeat),
                (int)Math.Ceiling((double)a.Quantity / repeats));

            for (var ci = 0; ci < copies; ci++)
            {
                var tag = $"a{ai}_p{ci}";
                var rotated = model.NewBoolVar($"rot_{tag}");
                if (!a.AllowRotation || w0 == h0) model.Add(rotated == 0);

                var w = model.NewIntVar(Math.Min(w0, h0), Math.Max(w0, h0), $"w_{tag}");
                var h = model.NewIntVar(Math.Min(w0, h0), Math.Max(w0, h0), $"h_{tag}");
                model.Add(w == w0 + (h0 - w0) * rotated);
                model.Add(h == h0 + (w0 - h0) * rotated);

                var x = model.NewIntVar(0, usableW, $"x_{tag}");
                var y = model.NewIntVar(0, usableH, $"y_{tag}");
                model.Add(x + w <= usableW);
                model.Add(y + h <= usableH);

                var ixEnd = model.NewIntVar(0, usableW + gap, $"ixe_{tag}");
                var iyEnd = model.NewIntVar(0, usableH + gap, $"iye_{tag}");
                xIntervals.Add(model.NewIntervalVar(x, w + gap, ixEnd, $"ix_{tag}"));
                yIntervals.Add(model.NewIntervalVar(y, h + gap, iyEnd, $"iy_{tag}"));
                pieces.Add(new PieceVars(ai, a.Name, rotated, x, y, w, h));
            }
        }

        if (xIntervals.Count > 1)

            if (xIntervals.Count > 1)
            {
                var noOverlap = model.AddNoOverlap2D();

                for (int i = 0; i < xIntervals.Count; i++)
                {
                    noOverlap.AddRectangle(xIntervals[i], yIntervals[i]);
                }
            }

        var solver = new CpSolver
        {
            StringParameters = $"max_time_in_seconds:{seconds.ToString(CultureInfo.InvariantCulture)} num_search_workers:8"
        };
        return new ModelResult(solver.Solve(model), solver, pieces);
    }

    private static ImpositionAlternative BuildResult(MixedImpositionRequest request, ModelResult result, int repeats)
    {
        var placements = result.Pieces.Select((p, i) => new ImpositionPlacement(
            i + 1,
            request.MarginMm + FromUnits(result.Solver.Value(p.X)),
            request.MarginMm + FromUnits(result.Solver.Value(p.Y)),
            FromUnits(result.Solver.Value(p.Width)),
            FromUnits(result.Solver.Value(p.Height)),
            p.Name, p.ArtworkIndex, result.Solver.Value(p.Rotated) == 1, null)).ToList();

        var perRepeat = request.Artworks.Select((a, i) =>
                new KeyValuePair<string, int>(a.Name, result.Pieces.Count(p => p.ArtworkIndex == i)))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        var planned = perRepeat.ToDictionary(x => x.Key, x => checked(x.Value * repeats), StringComparer.Ordinal);
        var usedPerRepeat = placements.Sum(p => p.Width * p.Height);
        var used = usedPerRepeat * repeats;
        var material = request.StockWidthMm * request.StockHeightMm * repeats;
        var waste = Math.Max(0, material - used);
        var status = result.Status == CpSolverStatus.Optimal ? "FEASIBLE — solver completed" : "FEASIBLE — time-limited";
        return new ImpositionAlternative(
            "Rectangular fixed-repeat layout", request.IsRoll ? "Roll" : "Sheet",
            placements.Any(p => p.Rotated), 0, 0, placements.Count, repeats,
            planned.Values.Sum(), material, request.StockWidthMm, request.StockHeightMm,
            used, waste, material == 0 ? 100 : Math.Round(waste / material * 100, 2),
            material == 0 ? 0 : Math.Round(used / material * 100, 2),
            request.IsRoll ? request.StockHeightMm * repeats : 0,
            placements, perRepeat, status, planned);
    }

    private static void ValidateRequest(MixedImpositionRequest r)
    {
        if (r.Artworks is null || r.Artworks.Count == 0)
            throw new ArgumentException("Add at least one artwork.");
        if (r.StockWidthMm <= 0 || r.StockHeightMm <= 0 || r.MarginMm < 0 || r.GapMm < 0 ||
            r.StockWidthMm <= 2 * r.MarginMm || r.StockHeightMm <= 2 * r.MarginMm)
            throw new ArgumentException("Enter positive stock dimensions and valid non-negative margins and gaps.");
        if (r.Artworks.Any(a => string.IsNullOrWhiteSpace(a.Name) || a.WidthMm <= 0 || a.HeightMm <= 0 ||
            a.Quantity <= 0 || a.MinimumPerRepeat < 0 || a.MinimumPerRepeat > a.Quantity))
            throw new ArgumentException("Every artwork needs a name, positive dimensions and quantity, and a valid minimum per repeat.");
        if (r.Artworks.Select(a => a.Name).Distinct(StringComparer.Ordinal).Count() != r.Artworks.Count)
            throw new ArgumentException("Artwork names must be unique.");
    }

    private static int ToUnits(decimal mm) => checked((int)Math.Round(mm * UnitsPerMm, MidpointRounding.AwayFromZero));
    private static decimal FromUnits(long units) => (decimal)units / UnitsPerMm;

    private sealed record PieceVars(int ArtworkIndex, string Name, BoolVar Rotated,
        IntVar X, IntVar Y, IntVar Width, IntVar Height);
    private sealed record ModelResult(CpSolverStatus Status, CpSolver Solver, List<PieceVars> Pieces);
}
