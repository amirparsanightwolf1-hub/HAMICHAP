using HamiChap_MES_ERP.Models.Imposition;

namespace HamiChap_MES_ERP.Services.Imposition;

/// <summary>
/// Local rectangular MaxRects nesting engine. It packs individual jobs by their own
/// quantities and can compare several placement heuristics. No network calls are made.
/// </summary>
internal static class RectangularNestingSolver
{
    private enum Heuristic { ShortSide, Area, BottomLeft, LongSide }

    private sealed record Part(int ArtworkIndex, string Name, decimal Width, decimal Height, bool Rotated);
    private sealed record Placement(Part Part, decimal X, decimal Y);
    private sealed record BinResult(List<Placement> Placements, decimal UsedArea, bool AnyRotation);
    private sealed record Score(Part Part, int FreeIndex, decimal X, decimal Y, decimal ShortSide,
        decimal LongSide, decimal Area, decimal Bottom, decimal Left);

    public static IReadOnlyList<ImpositionAlternative> Solve(MixedImpositionRequest request)
    {
        Validate(request);

        var usableW = request.StockWidthMm - 2m * request.MarginMm;
        var usableH = request.StockHeightMm - 2m * request.MarginMm;
        var maxRepeats = request.Artworks.Min(a => a.Quantity);
        var lowerBound = 1;

        foreach (var a in request.Artworks)
        {
            var normalAcross = CountFit(usableW, a.WidthMm, request.GapMm);
            var normalDown = CountFit(usableH, a.HeightMm, request.GapMm);
            var capacity = normalAcross * normalDown;
            if (a.AllowRotation)
                capacity = Math.Max(capacity,
                    CountFit(usableW, a.HeightMm, request.GapMm) *
                    CountFit(usableH, a.WidthMm, request.GapMm));

            if (capacity < 1)
                throw new ArgumentException($"Artwork '{a.Name}' cannot fit inside the usable stock area.");
            if (a.MinimumPerRepeat > capacity)
                throw new ArgumentException($"Minimum per sheet/repeat for '{a.Name}' exceeds its fit capacity.");

            lowerBound = Math.Max(lowerBound,
                (int)Math.Ceiling((double)a.Quantity / capacity));
        }

        if (lowerBound > maxRepeats)
            throw new ArgumentException("A fixed identical-repeat layout cannot meet the requested quantities with these stock dimensions.");

        var repeatCandidates = Enumerable.Range(lowerBound,
            Math.Min(6, maxRepeats - lowerBound + 1)).ToList();
        if (!repeatCandidates.Contains(maxRepeats))
            repeatCandidates.Add(maxRepeats);

        var heuristics = new[] { Heuristic.ShortSide, Heuristic.Area, Heuristic.BottomLeft, Heuristic.LongSide };
        var alternatives = new List<ImpositionAlternative>();

        foreach (var heuristic in heuristics)
        {
            foreach (var repeats in repeatCandidates)
            {
                var copies = request.Artworks.Select(a => Math.Max(
                    Math.Max(1, a.MinimumPerRepeat),
                    (int)Math.Ceiling((double)a.Quantity / repeats))).ToArray();

                if (copies.Sum(q => (long)q) > 10_000)
                    continue;

                var remaining = (int[])copies.Clone();
                var bin = PackOneBin(request, remaining, heuristic);
                if (remaining.Any(q => q > 0))
                    continue;

                var perRepeat = request.Artworks.Select((a, i) =>
                    new KeyValuePair<string, int>(a.Name, copies[i]))
                    .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
                var planned = perRepeat.ToDictionary(x => x.Key,
                    x => checked(x.Value * repeats), StringComparer.Ordinal);
                var used = bin.UsedArea * repeats;
                var material = request.StockWidthMm * request.StockHeightMm * repeats;
                var waste = Math.Max(0, material - used);
                var rollLength = request.IsRoll ? request.StockHeightMm * repeats : 0;

                alternatives.Add(Build(request, heuristic.ToString(), bin.Placements,
                    perRepeat, planned, repeats, planned.Values.Sum(), material,
                    used, waste, bin.AnyRotation, rollLength));
            }
        }

        if (alternatives.Count == 0)
            throw new InvalidOperationException("No identical rectangular sheet/repeat pattern could be found. Try a larger stock area, smaller gap, or lower minimum per repeat.");

        return alternatives
            .OrderBy(x => x.StockAreaMm2)
            .ThenBy(x => x.WastePercent)
            .ThenBy(x => x.ProducedQuantity)
            .GroupBy(x => string.Join("|", x.PlannedQuantityPerArtwork!.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{kv.Value}")) +
                          "|" + string.Join("|", x.Placements.Select(p => $"{p.ArtworkIndex}:{p.X}:{p.Y}:{p.Width}:{p.Height}")))
            .Select(g => g.First())
            .Take(4)
            .Select((x, i) => x with { Name = $"MaxRects option {i + 1} — {x.Name}" })
            .ToList();
    }

    private static int CountFit(decimal usable, decimal item, decimal gap) =>
        usable < item || item <= 0 ? 0 : (int)Math.Floor((double)((usable + gap) / (item + gap)));

    private static BinResult PackOneBin(MixedImpositionRequest r, int[] remaining, Heuristic heuristic)
    {
        var free = new List<FreeRect>
        {
            new(r.MarginMm, r.MarginMm,
                r.StockWidthMm - 2m * r.MarginMm + r.GapMm,
                r.StockHeightMm - 2m * r.MarginMm + r.GapMm)
        };
        var placed = new List<Placement>();
        decimal usedArea = 0;
        var anyRotation = false;

        while (remaining.Any(q => q > 0))
        {
            Score? best = null;
            for (var ai = 0; ai < r.Artworks.Count; ai++)
            {
                if (remaining[ai] <= 0) continue;
                var a = r.Artworks[ai];
                FindCandidate(new Part(ai, a.Name, a.WidthMm, a.HeightMm, false));
                if (a.AllowRotation && a.WidthMm != a.HeightMm)
                    FindCandidate(new Part(ai, a.Name, a.HeightMm, a.WidthMm, true));

                void FindCandidate(Part part)
                {
                    var packedW = part.Width + r.GapMm;
                    var packedH = part.Height + r.GapMm;
                    for (var fi = 0; fi < free.Count; fi++)
                    {
                        var f = free[fi];
                        if (packedW > f.Width + 0.0001m || packedH > f.Height + 0.0001m) continue;
                        var leftoverW = Math.Abs(f.Width - packedW);
                        var leftoverH = Math.Abs(f.Height - packedH);
                        var shortSide = Math.Min(leftoverW, leftoverH);
                        var longSide = Math.Max(leftoverW, leftoverH);
                        var score = new Score(part, fi, f.X, f.Y, shortSide, longSide,
                            f.Width * f.Height - packedW * packedH, f.Y + packedH, f.X);
                        if (best is null || IsBetter(score, best, heuristic)) best = score;
                    }
                }
            }

            if (best is null) break;
            var chosen = best;
            var actual = new Placement(chosen.Part, chosen.X, chosen.Y);
            placed.Add(actual);
            remaining[chosen.Part.ArtworkIndex]--;
            usedArea += chosen.Part.Width * chosen.Part.Height;
            anyRotation |= chosen.Part.Rotated;

            var usedW = chosen.Part.Width + r.GapMm;
            var usedH = chosen.Part.Height + r.GapMm;
            SplitFreeRectangles(free, new FreeRect(chosen.X, chosen.Y, usedW, usedH));
            PruneFreeRectangles(free);
        }

        return new BinResult(placed, usedArea, anyRotation);
    }

    private static bool IsBetter(Score a, Score b, Heuristic h) => h switch
    {
        Heuristic.Area => a.Area < b.Area || (a.Area == b.Area && a.ShortSide < b.ShortSide),
        Heuristic.BottomLeft => a.Bottom < b.Bottom || (a.Bottom == b.Bottom && a.Left < b.Left),
        Heuristic.LongSide => a.LongSide < b.LongSide || (a.LongSide == b.LongSide && a.ShortSide < b.ShortSide),
        _ => a.ShortSide < b.ShortSide || (a.ShortSide == b.ShortSide && a.LongSide < b.LongSide)
    };

    private static void SplitFreeRectangles(List<FreeRect> free, FreeRect used)
    {
        var additions = new List<FreeRect>();
        for (var i = free.Count - 1; i >= 0; i--)
        {
            var f = free[i];
            if (!Intersects(f, used)) continue;
            free.RemoveAt(i);

            if (used.X > f.X && used.X < f.Right)
                additions.Add(new FreeRect(f.X, f.Y, used.X - f.X, f.Height));
            if (used.Right < f.Right && used.Right > f.X)
                additions.Add(new FreeRect(used.Right, f.Y, f.Right - used.Right, f.Height));
            if (used.Y > f.Y && used.Y < f.Bottom)
                additions.Add(new FreeRect(f.X, f.Y, f.Width, used.Y - f.Y));
            if (used.Bottom < f.Bottom && used.Bottom > f.Y)
                additions.Add(new FreeRect(f.X, used.Bottom, f.Width, f.Bottom - used.Bottom));
        }
        free.AddRange(additions.Where(x => x.Width > 0.0001m && x.Height > 0.0001m));
    }

    private static void PruneFreeRectangles(List<FreeRect> free)
    {
        for (var i = free.Count - 1; i >= 0; i--)
        {
            for (var j = 0; j < free.Count; j++)
            {
                if (i == j) continue;
                if (Contains(free[j], free[i]))
                {
                    free.RemoveAt(i);
                    break;
                }
            }
        }
    }

    private static bool Intersects(FreeRect a, FreeRect b) =>
        b.X < a.Right && b.Right > a.X && b.Y < a.Bottom && b.Bottom > a.Y;
    private static bool Contains(FreeRect a, FreeRect b) =>
        b.X >= a.X && b.Y >= a.Y && b.Right <= a.Right && b.Bottom <= a.Bottom;

    private static ImpositionAlternative Build(MixedImpositionRequest r, string heuristic,
        IReadOnlyList<Placement> placements, IReadOnlyDictionary<string, int> perRepeat,
        IReadOnlyDictionary<string, int> planned, int repeats, int produced,
        decimal material, decimal used, decimal waste, bool rotated, decimal rollLength)
    {
        var preview = placements.Select((p, i) => new ImpositionPlacement(
            i + 1, p.X, p.Y, p.Part.Width, p.Part.Height, p.Part.Name,
            p.Part.ArtworkIndex, p.Part.Rotated, null)).ToList();
        return new ImpositionAlternative(
            $"MaxRects {heuristic}", r.IsRoll ? "Roll" : "Sheet", rotated,
            0, 0, preview.Count, repeats, produced, material,
            r.StockWidthMm, r.StockHeightMm, used, waste,
            material <= 0 ? 100 : Math.Round(waste / material * 100m, 2),
            material <= 0 ? 0 : Math.Round(used / material * 100m, 2),
            rollLength, preview, perRepeat, "LOCAL HEURISTIC — rectangular nesting", planned);
    }

    private static void Validate(MixedImpositionRequest r)
    {
        if (r.Artworks is null || r.Artworks.Count == 0)
            throw new ArgumentException("Add at least one artwork.");
        if (r.StockWidthMm <= 0 || r.StockHeightMm <= 0 || r.MarginMm < 0 || r.GapMm < 0 ||
            r.StockWidthMm <= 2m * r.MarginMm || r.StockHeightMm <= 2m * r.MarginMm)
            throw new ArgumentException("Enter valid stock dimensions, margins and gaps.");
        if (r.Artworks.Any(a => string.IsNullOrWhiteSpace(a.Name) || a.WidthMm <= 0 ||
            a.HeightMm <= 0 || a.Quantity <= 0 || a.MinimumPerRepeat < 0 ||
            a.MinimumPerRepeat > a.Quantity))
            throw new ArgumentException("Each artwork needs a name, positive dimensions and quantity, and a valid minimum per repeat.");
        if (r.Artworks.Select(a => a.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != r.Artworks.Count)
            throw new ArgumentException("Artwork names must be unique.");

        var usableW = r.StockWidthMm - 2m * r.MarginMm;
        var usableH = r.StockHeightMm - 2m * r.MarginMm;
        foreach (var a in r.Artworks)
        {
            var fitsNormal = a.WidthMm <= usableW && a.HeightMm <= usableH;
            var fitsRotated = a.AllowRotation && a.HeightMm <= usableW && a.WidthMm <= usableH;
            if (!fitsNormal && !fitsRotated)
                throw new ArgumentException($"Artwork '{a.Name}' cannot fit inside the usable stock area, even when rotated.");
        }
    }

    private sealed record FreeRect(decimal X, decimal Y, decimal Width, decimal Height)
    {
        public decimal Right => X + Width;
        public decimal Bottom => Y + Height;
    }
}
