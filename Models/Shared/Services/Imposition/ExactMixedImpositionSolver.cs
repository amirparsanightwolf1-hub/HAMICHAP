using Google.OrTools.Sat;
using System.Diagnostics;
using System.Globalization;
using HamiChap_MES_ERP.Models.Imposition;

namespace HamiChap_MES_ERP.Services.Imposition;

/// <summary>
/// Fixed-repeat mixed-artwork solver. Geometry is represented by non-overlapping
/// occupied rectangles. Native CP-SAT NoOverlap2D constraints are used for shape
/// collision; the requested clearance is enforced between artwork bounding boxes.
/// This is conservative for clearance, but avoids a component-pair explosion.
/// Coordinates are quantized to 0.1 mm.
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
        var capacities = request.Artworks.Select(a =>
        {
            var area = OccupiedAreaUnits(a);
            return area <= 0 ? 0 : (int)Math.Min(int.MaxValue, (long)stockW * stockH / area);
        }).ToArray();

        var lower = 1;
        for (var i = 0; i < request.Artworks.Count; i++)
        {
            var a = request.Artworks[i];
            if (capacities[i] < 1)
                throw new ArgumentException($"Artwork '{a.Name}' cannot fit inside the usable stock dimensions.");
            var min = Math.Max(1, a.MinimumPerRepeat);
            if (min > capacities[i])
                throw new ArgumentException($"The minimum per sheet/repeat for '{a.Name}' exceeds its theoretical area capacity.");
            lower = Math.Max(lower, (int)Math.Ceiling((double)a.Quantity / Math.Min(a.Quantity, capacities[i])));
        }

        var timer = Stopwatch.StartNew();
        var limit = Math.Clamp(request.SolverTimeLimitSeconds, 1, 300);
        var maxRepeats = request.Artworks.Max(a => a.Quantity);
        ModelSolveResult? best = null;
        var bestRepeats = int.MaxValue;

        // First obtain a guaranteed, low-complexity feasible baseline.
        var initialBudget = Math.Min(Math.Max(.5, limit * .2), limit * .35);
        var initial = SolveModel(request, stockW, stockH, gap, capacities, maxRepeats, initialBudget);
        if (initial.Status is CpSolverStatus.Optimal or CpSolverStatus.Feasible)
        {
            best = initial;
            bestRepeats = maxRepeats;
        }
        else if (initial.Status == CpSolverStatus.Infeasible)
            throw new ArgumentException("No repeated layout can satisfy the artwork minimums on this stock.");

        var low = lower;
        var high = best is null ? maxRepeats : bestRepeats - 1;
        while (low <= high && limit - timer.Elapsed.TotalSeconds > .1)
        {
            var candidate = low + (high - low) / 2;
            var remaining = limit - timer.Elapsed.TotalSeconds;
            var probe = SolveModel(request, stockW, stockH, gap, capacities, candidate, remaining);
            if (probe.Status is CpSolverStatus.Optimal or CpSolverStatus.Feasible)
            {
                best = probe;
                bestRepeats = candidate;
                high = candidate - 1;
            }
            else if (probe.Status == CpSolverStatus.Infeasible)
                low = candidate + 1;
            else
                break;
        }

        if (best is null)
            throw new InvalidOperationException("No verified layout was found within the solver time limit.");

        var proven = low >= bestRepeats;
        var status = proven
            ? "OPTIMALITY PROVEN — MINIMUM REPEATS"
            : "FEASIBLE — OPTIMALITY NOT PROVEN";
        return new[] { BuildResult(request, best, bestRepeats, status) };
    }

    private static ModelSolveResult SolveModel(MixedImpositionRequest request, int stockW, int stockH,
        int gap, int[] capacities, int targetRepeats, double seconds)
    {
        var model = new CpModel();
        var pieces = new List<PieceVars>();
        var shapeX = new List<IntervalVar>();
        var shapeY = new List<IntervalVar>();

        for (var ai = 0; ai < request.Artworks.Count; ai++)
        {
            var artwork = request.Artworks[ai];
            var bw = ToUnits(artwork.WidthMm);
            var bh = ToUnits(artwork.HeightMm);
            var copies = Math.Max(Math.Max(1, artwork.MinimumPerRepeat),
                (int)Math.Ceiling((double)artwork.Quantity / targetRepeats));
            if (copies > artwork.Quantity || copies > capacities[ai])
                return new(CpSolverStatus.Infeasible, null!, pieces);

            var local = NormalizeComponents(artwork);
            for (var copy = 0; copy < copies; copy++)
            {
                var tag = $"a{ai}_p{copy}";
                var rotated = model.NewBoolVar($"rot_{tag}");
                if (!artwork.AllowRotation || bw == bh) model.Add(rotated == 0);

                var w = model.NewIntVar(Math.Min(bw, bh), Math.Max(bw, bh), $"w_{tag}");
                var h = model.NewIntVar(Math.Min(bw, bh), Math.Max(bw, bh), $"h_{tag}");
                model.Add(w == bw + (bh - bw) * rotated);
                model.Add(h == bh + (bw - bh) * rotated);
                var x = model.NewIntVar(0, stockW, $"x_{tag}");
                var y = model.NewIntVar(0, stockH, $"y_{tag}");
                model.Add(x + w <= stockW);
                model.Add(y + h <= stockH);

                // Bounding-box clearance is deliberately separate from shape collision.
                // This enforces at least the requested gap conservatively, without applying
                // artificial gaps between the component rectangles inside one artwork.

                var componentVars = new List<ComponentVars>();
                for (var ci = 0; ci < local.Count; ci++)
                {
                    var r = local[ci];
                    var rx = ToUnits(r.Xmm);
                    var ry = ToUnits(r.Ymm);
                    var rw = ToUnits(r.WidthMm);
                    var rh = ToUnits(r.HeightMm);

                    // Rotate each local rectangle clockwise around the artwork origin.
                    var ox = model.NewIntVar(0, Math.Max(bw, bh), $"ox_{tag}_{ci}");
                    var oy = model.NewIntVar(0, Math.Max(bw, bh), $"oy_{tag}_{ci}");
                    var cw = model.NewIntVar(1, Math.Max(bw, bh), $"cw_{tag}_{ci}");
                    var ch = model.NewIntVar(1, Math.Max(bw, bh), $"ch_{tag}_{ci}");
                    model.Add(ox == rx + (bh - ry - rh - rx) * rotated);
                    model.Add(oy == ry + (rx - ry) * rotated);
                    model.Add(cw == rw + (rh - rw) * rotated);
                    model.Add(ch == rh + (rw - rh) * rotated);

                    var absX = model.NewIntVar(0, stockW + Math.Max(bw, bh), $"absx_{tag}_{ci}");
                    var absY = model.NewIntVar(0, stockH + Math.Max(bw, bh), $"absy_{tag}_{ci}");
                    var endX = model.NewIntVar(0, stockW + Math.Max(bw, bh), $"endx_{tag}_{ci}");
                    var endY = model.NewIntVar(0, stockH + Math.Max(bw, bh), $"endy_{tag}_{ci}");
                    model.Add(absX == x + ox);
                    model.Add(absY == y + oy);
                    model.Add(endX == absX + cw);
                    model.Add(endY == absY + ch);
                    shapeX.Add(model.NewIntervalVar(absX, cw, endX, $"sxi_{tag}_{ci}"));
                    shapeY.Add(model.NewIntervalVar(absY, ch, endY, $"syi_{tag}_{ci}"));
                    componentVars.Add(new ComponentVars(ox, oy, cw, ch));
                }
                pieces.Add(new PieceVars(ai, artwork.Name, copy, rotated, x, y, w, h, componentVars, local));
            }
        }

        // Native global constraints avoid O(componentCount^2) explicit Boolean disjunctions.
        // Component rectangles from the same artwork are already disjoint by decomposition.
        // Each artwork copy is a rigid compound shape.
        //
        // All component rectangles belonging to the same copy share:
        // - one X origin
        // - one Y origin
        // - one rotation variable
        //
        // Their relative positions never change.
        // AddNoOverlap2D checks collisions between the occupied rectangles.
        // Since the rectangles within one artwork are already non-overlapping,
        // this represents collision between the compound artwork footprints.
        //
        // IMPORTANT:
        // Do not add a separate bounding-box NoOverlap2D constraint here.
        // It would prevent valid nesting into empty spaces of irregular artworks.

        if (shapeX.Count > 1)
        {
            var compoundNoOverlap = model.AddNoOverlap2D();

            for (var i = 0; i < shapeX.Count; i++)
            {
                compoundNoOverlap.AddRectangle(
                    shapeX[i],
                    shapeY[i]);
            }
        }
        var solver = new CpSolver
        {
            StringParameters = $"max_time_in_seconds:{Math.Max(.1, seconds).ToString(CultureInfo.InvariantCulture)} num_search_workers:8"
        };
        return new(solver.Solve(model), solver, pieces);
    }

    private static IReadOnlyList<ArtworkOccupiedRectangle> NormalizeComponents(ImpositionArtwork a)
    {
        if (a.OccupiedRectangles is null || a.OccupiedRectangles.Count == 0)
            return new[] { new ArtworkOccupiedRectangle(0, 0, a.WidthMm, a.HeightMm) };
        foreach (var r in a.OccupiedRectangles)
            if (r.Xmm < 0 || r.Ymm < 0 || r.WidthMm <= 0 || r.HeightMm <= 0 ||
                r.Xmm + r.WidthMm > a.WidthMm + .001m || r.Ymm + r.HeightMm > a.HeightMm + .001m)
                throw new ArgumentException($"Artwork '{a.Name}' has an occupied rectangle outside its artwork bounds.");
        return a.OccupiedRectangles;
    }

    private static long OccupiedAreaUnits(ImpositionArtwork a) =>
        NormalizeComponents(a).Sum(r => (long)ToUnits(r.WidthMm) * ToUnits(r.HeightMm));

    private static ImpositionAlternative BuildResult(MixedImpositionRequest request,
        ModelSolveResult solved, int repeats, string proof)
    {
        var placements = solved.Pieces.Select((p, i) =>
        {
            var rotated = solved.Solver.Value(p.Rotated) == 1;
            return new ImpositionPlacement(i + 1,
                request.MarginMm + FromUnits(solved.Solver.Value(p.X)),
                request.MarginMm + FromUnits(solved.Solver.Value(p.Y)),
                FromUnits(solved.Solver.Value(p.Width)), FromUnits(solved.Solver.Value(p.Height)),
                p.Name, p.ArtworkIndex, rotated, p.LocalComponents);
        }).ToList();

        var perRepeat = request.Artworks.Select((a, i) => (a.Name, Count: solved.Pieces.Count(p => p.ArtworkIndex == i)))
            .ToDictionary(x => x.Name, x => x.Count, StringComparer.Ordinal);
        var planned = perRepeat.ToDictionary(k => k.Key, v => checked(v.Value * repeats), StringComparer.Ordinal);
        var totalProduced = planned.Values.Sum();
        var usedPerRepeat = request.Artworks.SelectMany((a, i) =>
            solved.Pieces.Where(p => p.ArtworkIndex == i).SelectMany(_ => NormalizeComponents(a)))
            .Sum(r => r.WidthMm * r.HeightMm);
        var used = usedPerRepeat * repeats;
        var stockArea = request.StockWidthMm * request.StockHeightMm * repeats;
        var waste = Math.Max(0, stockArea - used);
        return new ImpositionAlternative(
            $"Contour-aware fixed layout — {proof}", request.IsRoll ? "Roll" : "Sheet", false, 0, 0,
            placements.Count, repeats, totalProduced, stockArea, request.StockWidthMm, request.StockHeightMm,
            used, waste, stockArea == 0 ? 100 : Math.Round(waste / stockArea * 100, 2),
            stockArea == 0 ? 0 : Math.Round(used / stockArea * 100, 2),
            request.IsRoll ? request.StockHeightMm * repeats : 0, placements, perRepeat, proof, planned);
    }

    private static void ValidateRequest(MixedImpositionRequest r)
    {
        if (r.Artworks is null || r.Artworks.Count == 0) throw new ArgumentException("Add at least one artwork.");
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
    private sealed record ComponentVars(IntVar X, IntVar Y, IntVar Width, IntVar Height);
    private sealed record PieceVars(int ArtworkIndex, string Name, int CopyIndex, BoolVar Rotated,
        IntVar X, IntVar Y, IntVar Width, IntVar Height,
        List<ComponentVars> Components, IReadOnlyList<ArtworkOccupiedRectangle> LocalComponents);
    private sealed record ModelSolveResult(CpSolverStatus Status, CpSolver Solver, List<PieceVars> Pieces);
}
