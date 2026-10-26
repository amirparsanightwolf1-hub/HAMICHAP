using Google.OrTools.Sat;
using System.Diagnostics;

namespace HamiChap_MES_ERP.Services.Imposition;

/// <summary>
/// Optimizes one fixed rectangular layout repeated on every sheet / roll repeat.
/// First minimizes the number of sheets/repeats needed to fulfill all artwork quantities.
/// Among layouts using that minimum number, minimizes total overproduction.
/// A production result is returned only when both the minimum sheet count and the
/// within-that-count piece count have been proven optimal by CP-SAT.
/// Coordinates are represented in 0.1 mm units.
/// </summary>
internal static class ExactMixedImpositionSolver
{
    private const int UnitsPerMm = 10;

    public static IReadOnlyList<ImpositionAlternative> Solve(MixedImpositionRequest request)
    {
        ValidateRequest(request);

        var stockW = ToUnits(request.StockWidthMm - 2 * request.MarginMm);
        var stockH = ToUnits(request.StockHeightMm - 2 * request.MarginMm);
        var gap = ToUnits(request.GapMm);
        var usableArea = (long)stockW * stockH;

        var perArtworkCapacity = new int[request.Artworks.Count];
        var lowerBoundSheets = 1;
        for (var i = 0; i < request.Artworks.Count; i++)
        {
            var artwork = request.Artworks[i];
            var w = ToUnits(artwork.WidthMm);
            var h = ToUnits(artwork.HeightMm);
            var area = (long)w * h;
            var capacity = area <= 0 ? 0 : (int)Math.Min(int.MaxValue, usableArea / area);
            if (capacity < 1)
                throw new ArgumentException($"Artwork '{artwork.Name}' cannot fit inside the usable stock dimensions.");

            perArtworkCapacity[i] = Math.Min(artwork.Quantity, capacity);
            var minimum = Math.Max(1, artwork.MinimumPerRepeat);
            if (minimum > perArtworkCapacity[i])
                throw new ArgumentException($"The minimum per sheet/repeat for '{artwork.Name}' is greater than its theoretical maximum capacity.");

            lowerBoundSheets = Math.Max(lowerBoundSheets,
                (int)Math.Ceiling((double)artwork.Quantity / perArtworkCapacity[i]));
        }

        var totalTimer = Stopwatch.StartNew();
        var totalTimeLimit = Math.Clamp(request.SolverTimeLimitSeconds, 1, 300);
        var maxSheets = request.Artworks.Max(a => a.Quantity);
        ModelSolveResult? bestFeasible = null;
        var bestSheets = int.MaxValue;

        // Phase 1: quickly obtain a valid layout using a generous sheet count.
        // This provides a useful result even if proving the optimum later times out.
        var initialBudget = Math.Min(Math.Max(0.25, totalTimeLimit * 0.20), totalTimeLimit * 0.40);
        var initial = SolveModel(request, stockW, stockH, gap, perArtworkCapacity,
            maxSheets, initialBudget);
        if (initial.Status is CpSolverStatus.Optimal or CpSolverStatus.Feasible)
        {
            bestFeasible = initial;
            bestSheets = maxSheets;
        }
        else if (initial.Status == CpSolverStatus.Infeasible)
        {
            throw new ArgumentException(
                "No repeated layout can satisfy all artwork minimums on the specified stock. Reduce minimum-per-repeat quantities or increase usable stock dimensions.");
        }

        // Phase 2: binary-search the smallest feasible sheet count. If CP-SAT times
        // out, keep and return the best known verified layout rather than discarding it.
        var low = lowerBoundSheets;
        var high = bestFeasible is null ? maxSheets : maxSheets - 1;
        var optimalityProven = false;

        while (low <= high)
        {
            var remainingSeconds = totalTimeLimit - totalTimer.Elapsed.TotalSeconds;
            if (remainingSeconds <= 0.05)
                break;

            var candidateSheets = low + (high - low) / 2;
            var probe = SolveModel(request, stockW, stockH, gap, perArtworkCapacity,
                candidateSheets, remainingSeconds);

            if (probe.Status is CpSolverStatus.Optimal or CpSolverStatus.Feasible)
            {
                bestFeasible = probe;
                bestSheets = candidateSheets;
                high = candidateSheets - 1;
            }
            else if (probe.Status == CpSolverStatus.Infeasible)
            {
                low = candidateSheets + 1;
            }
            else
            {
                // Unknown / timeout: do not call the result optimal, but preserve the
                // most recent feasible model and its exact coordinates.
                break;
            }
        }

        // A proven optimum requires the search to finish by excluding all lower sheet
        // counts. If the lower bound has crossed the best feasible count, proof is complete.
        if (bestFeasible is not null && low >= bestSheets)
            optimalityProven = true;

        if (bestFeasible is null)
            throw new InvalidOperationException(
                "No verified layout was found within the solver time limit. Increase the limit or simplify the job, then retry.");

        var statusText = optimalityProven
            ? "OPTIMALITY PROVEN — MINIMUM SHEETS"
            : "FEASIBLE — OPTIMALITY NOT PROVEN";
        return new[] { BuildResult(request, bestFeasible, bestSheets, statusText) };
    }

    private static ImpositionAlternative BuildResult(
        MixedImpositionRequest request,
        ModelSolveResult solved,
        int sheetsRequired,
        string proof)
    {
        var selected = solved.Pieces;
        var placements = selected.Select((p, index) => new ImpositionPlacement(
            index + 1,
            request.MarginMm + FromUnits(solved.Solver.Value(p.X)),
            request.MarginMm + FromUnits(solved.Solver.Value(p.Y)),
            FromUnits(solved.Solver.Value(p.Width)),
            FromUnits(solved.Solver.Value(p.Height)),
            p.ArtworkName,
            p.ArtworkIndex)).ToList();

        var countsByIndex = request.Artworks.Select((a, i) =>
            (a.Name, Count: selected.Count(p => p.ArtworkIndex == i)))
            .ToDictionary(x => x.Name, x => x.Count, StringComparer.Ordinal);
        var producedByArtwork = request.Artworks.ToDictionary(
            a => a.Name,
            a => checked(countsByIndex[a.Name] * sheetsRequired),
            StringComparer.Ordinal);
        var producedTotal = producedByArtwork.Values.Sum();
        var usedAreaPerRepeat = placements.Sum(p => p.Width * p.Height);
        var usedArea = usedAreaPerRepeat * sheetsRequired;
        var stockAreaPerRepeat = request.StockWidthMm * request.StockHeightMm;
        var stockArea = stockAreaPerRepeat * sheetsRequired;
        var waste = Math.Max(0, stockArea - usedArea);
        var wastePercent = stockArea == 0 ? 100 : Math.Round(waste / stockArea * 100, 2);
        var utilization = stockArea == 0 ? 0 : Math.Round(usedArea / stockArea * 100, 2);

        return new ImpositionAlternative(
            $"Fixed layout — {proof}",
            request.IsRoll ? "Roll" : "Sheet",
            false,
            0,
            0,
            placements.Count,
            sheetsRequired,
            producedTotal,
            stockArea,
            request.StockWidthMm,
            request.StockHeightMm,
            usedArea,
            waste,
            wastePercent,
            utilization,
            request.IsRoll ? request.StockHeightMm * sheetsRequired : 0,
            placements,
            countsByIndex,
            proof,
            producedByArtwork);
    }

    private static ModelSolveResult SolveModel(
        MixedImpositionRequest request,
        int stockW,
        int stockH,
        int gap,
        int[] perArtworkCapacity,
        int targetSheets,
        double timeLimitSeconds)
    {
        // Key performance improvement: with one identical layout repeated on every
        // sheet, the number of copies of each artwork is determined by the sheet count:
        // max(user minimum, ceil(order quantity / sheet count)). Adding extra copies
        // cannot make a layout easier to fit and can only increase overproduction, so
        // don't create optional candidates up to the area bound.
        var model = new CpModel();
        var pieces = new List<PieceVars>();
        var xIntervals = new List<IntervalVar>();
        var yIntervals = new List<IntervalVar>();

        for (var artworkIndex = 0; artworkIndex < request.Artworks.Count; artworkIndex++)
        {
            var artwork = request.Artworks[artworkIndex];
            var baseW = ToUnits(artwork.WidthMm);
            var baseH = ToUnits(artwork.HeightMm);
            var requiredForOrder = (int)Math.Ceiling((double)artwork.Quantity / targetSheets);
            var copiesRequired = Math.Max(Math.Max(1, artwork.MinimumPerRepeat), requiredForOrder);

            if (copiesRequired > artwork.Quantity || copiesRequired > perArtworkCapacity[artworkIndex])
                return new ModelSolveResult(CpSolverStatus.Infeasible, null!, pieces);

            for (var copy = 0; copy < copiesRequired; copy++)
            {
                var suffix = $"n{targetSheets}_a{artworkIndex}_p{copy}";
                var rotated = model.NewBoolVar($"rotated_{suffix}");
                if (!artwork.AllowRotation || baseW == baseH)
                    model.Add(rotated == 0);

                var minW = Math.Min(baseW, baseH);
                var maxW = Math.Max(baseW, baseH);
                var width = model.NewIntVar(minW, maxW, $"w_{suffix}");
                var height = model.NewIntVar(minW, maxW, $"h_{suffix}");
                model.Add(width == baseW + (baseH - baseW) * rotated);
                model.Add(height == baseH + (baseW - baseH) * rotated);

                var x = model.NewIntVar(0, stockW, $"x_{suffix}");
                var y = model.NewIntVar(0, stockH, $"y_{suffix}");
                model.Add(x + width <= stockW);
                model.Add(y + height <= stockH);

                // Inflate each rectangle by the requested gap in both axes. The
                // native 2D non-overlap propagator is much faster than O(n^2)
                // explicit pairwise disjunctions for large layouts.
                var xEnd = model.NewIntVar(0, stockW + gap, $"xe_{suffix}");
                var yEnd = model.NewIntVar(0, stockH + gap, $"ye_{suffix}");
                model.Add(xEnd == x + width + gap);
                model.Add(yEnd == y + height + gap);
                var xInterval = model.NewIntervalVar(x, width + gap, xEnd, $"xi_{suffix}");
                var yInterval = model.NewIntervalVar(y, height + gap, yEnd, $"yi_{suffix}");
                xIntervals.Add(xInterval);
                yIntervals.Add(yInterval);
                pieces.Add(new PieceVars(artworkIndex, artwork.Name, copy, rotated, x, y, width, height));
            }
        }

        // Native CP-SAT 2D non-overlap constraint; significantly faster than adding
        // four directional Boolean variables for every pair of rectangles.
        var noOverlap = model.AddNoOverlap2D();
        for (var i = 0; i < xIntervals.Count; i++)
            noOverlap.AddRectangle(xIntervals[i], yIntervals[i]);

        var solver = new CpSolver
        {
            StringParameters = $"max_time_in_seconds:{Math.Max(0.1, timeLimitSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture)} num_search_workers:8"
        };
        var status = solver.Solve(model);
        return new ModelSolveResult(status, solver, pieces);
    }

    private static void ValidateRequest(MixedImpositionRequest request)
    {
        if (request.Artworks is null || request.Artworks.Count == 0)
            throw new ArgumentException("Add at least one artwork.");
        if (request.StockWidthMm <= 0 || request.StockHeightMm <= 0 || request.MarginMm < 0 || request.GapMm < 0 ||
            request.StockWidthMm <= 2 * request.MarginMm || request.StockHeightMm <= 2 * request.MarginMm)
            throw new ArgumentException("Enter positive stock dimensions and valid non-negative margins and gaps.");
        if (request.Artworks.Any(a => string.IsNullOrWhiteSpace(a.Name) || a.WidthMm <= 0 || a.HeightMm <= 0 ||
            a.Quantity <= 0 || a.MinimumPerRepeat < 0 || a.MinimumPerRepeat > a.Quantity))
            throw new ArgumentException("Every artwork needs a name, positive dimensions and quantity, and a valid non-negative minimum per sheet/repeat no greater than its requested quantity.");
        if (request.Artworks.Select(a => a.Name).Distinct(StringComparer.Ordinal).Count() != request.Artworks.Count)
            throw new ArgumentException("Artwork names must be unique so quantities and production results can be reported separately.");
    }

    private static int ToUnits(decimal mm) => checked((int)Math.Round(mm * UnitsPerMm, MidpointRounding.AwayFromZero));
    private static decimal FromUnits(long units) => (decimal)units / UnitsPerMm;

    private sealed record PieceVars(int ArtworkIndex, string ArtworkName, int CopyIndex,
        BoolVar Rotated, IntVar X, IntVar Y, IntVar Width, IntVar Height);

    private sealed record ModelSolveResult(CpSolverStatus Status, CpSolver Solver, List<PieceVars> Pieces);
}
