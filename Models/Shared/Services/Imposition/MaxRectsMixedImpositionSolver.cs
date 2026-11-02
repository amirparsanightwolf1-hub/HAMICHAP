using System.Diagnostics;
using HamiChap_MES_ERP.Models.Imposition;
using RectangleBinPacking;

namespace HamiChap_MES_ERP.Services.Imposition;

/// <summary>
/// Mixed rectangular imposition using the open-source MaxRects implementation
/// from RectangleBinPack.CSharp. A single sheet/repeat pattern is repeated.
/// MaxRects is heuristic: several item orders, placement heuristics and legal
/// orientation combinations are tested, then the lowest feasible repeat count
/// found within the time budget is returned.
/// </summary>
internal static class MaxRectsMixedImpositionSolver
{
    private const int UnitsPerMm = 10; // 0.1 mm coordinates
    private const int MaxCopiesPerRepeat = 2500;
    private const int MaxArtworkTypesForFullOrientationSweep = 8;

    private static readonly FreeRectChoiceHeuristic[] Heuristics =
    {
        FreeRectChoiceHeuristic.RectBestShortSideFit,
        FreeRectChoiceHeuristic.RectBestAreaFit,
        FreeRectChoiceHeuristic.RectBottomLeftRule,
        FreeRectChoiceHeuristic.RectContactPointRule,
        FreeRectChoiceHeuristic.RectBestLongSideFit
    };

    public static IReadOnlyList<ImpositionAlternative> Solve(MixedImpositionRequest request)
    {
        ValidateRequest(request);

        var usableWidth = ToUnits(request.StockWidthMm - 2m * request.MarginMm);
        var usableHeight = ToUnits(request.StockHeightMm - 2m * request.MarginMm);
        var gap = ToUnits(request.GapMm);
        var maxRepeats = request.Artworks.Min(a => a.Quantity);
        var timeLimit = Math.Clamp(request.SolverTimeLimitSeconds, 1, 300);
        var timer = Stopwatch.StartNew();

        ValidateIndividualFit(request, usableWidth, usableHeight);
        var lowerBound = FindAreaLowerBound(request, usableWidth, usableHeight, gap, maxRepeats);
        if (lowerBound > maxRepeats)
            throw new ArgumentException("The quantities cannot fit into one identical pattern repeated across the material.");

        // Establish a verified fallback first: at maxRepeats, every artwork needs
        // only one copy in the repeated pattern (unless MinimumPerRepeat is higher).
        var fallback = TryPackCandidate(request, usableWidth, usableHeight, gap, maxRepeats,
            Math.Max(1, timeLimit / 4.0), timer);
        if (fallback is null)
        {
            throw new ArgumentException(
                "MaxRects could not fit at least the minimum required copies of every artwork on one repeat. " +
                "Check the usable sheet size, margins, gaps and minimum-per-repeat values. " +
                "This is a heuristic failure, not proof that no geometric arrangement exists.");
        }

        var best = fallback;
        var bestRepeats = maxRepeats;
        var ascendingSearchFoundCandidate = false;

        // Search upward from an area-derived lower bound. This avoids assuming that
        // a heuristic's success/failure is mathematically monotonic.
        for (var repeats = lowerBound; repeats < maxRepeats; repeats++)
        {
            var remaining = timeLimit - timer.Elapsed.TotalSeconds;
            if (remaining <= 0.05)
                break;

            var candidate = TryPackCandidate(
                request, usableWidth, usableHeight, gap, repeats,
                Math.Min(remaining, Math.Max(0.15, timeLimit / 8.0)), timer);

            if (candidate is null)
                continue;

            best = candidate;
            bestRepeats = repeats;
            ascendingSearchFoundCandidate = true;
            break; // ascending scan: first feasible candidate is the best found
        }

        var result = BuildResult(request, best, bestRepeats, ascendingSearchFoundCandidate);
        ValidateSolvedLayout(request, result);
        return new[] { result };
    }

    private static Candidate? TryPackCandidate(
        MixedImpositionRequest request,
        int usableWidth,
        int usableHeight,
        int gap,
        int repeats,
        double seconds,
        Stopwatch totalTimer)
    {
        var requiredPerArtwork = request.Artworks
            .Select(a => Math.Max(Math.Max(1, a.MinimumPerRepeat), DivideRoundUp(a.Quantity, repeats)))
            .ToArray();

        var totalPieces = requiredPerArtwork.Sum();
        if (totalPieces <= 0 || totalPieces > MaxCopiesPerRepeat)
            return null;

        var basePieces = new List<Piece>(totalPieces);
        for (var artworkIndex = 0; artworkIndex < request.Artworks.Count; artworkIndex++)
        {
            var artwork = request.Artworks[artworkIndex];
            var width = ToUnits(artwork.WidthMm);
            var height = ToUnits(artwork.HeightMm);
            for (var copy = 0; copy < requiredPerArtwork[artworkIndex]; copy++)
                basePieces.Add(new Piece(artworkIndex, copy, width, height));
        }

        var orientationPlans = BuildOrientationPlans(request.Artworks);
        var orderings = BuildOrderings(basePieces);
        var candidateTimer = Stopwatch.StartNew();
        foreach (var orientationPlan in orientationPlans)
        foreach (var ordering in orderings)
        foreach (var heuristic in Heuristics)
        {
            if (candidateTimer.Elapsed.TotalSeconds >= seconds ||
                totalTimer.Elapsed.TotalSeconds >= Math.Clamp(request.SolverTimeLimitSeconds, 1, 300))
                return null;

            var packer = new MaxRectsBinPack(usableWidth + gap, usableHeight + gap, allowRotations: false);
            var placements = new List<PlacedPiece>(ordering.Count);
            var failed = false;

            foreach (var piece in ordering)
            {
                var forceRotated = orientationPlan[piece.ArtworkIndex];
                var width = forceRotated ? piece.Height : piece.Width;
                var height = forceRotated ? piece.Width : piece.Height;
                var packed = packer.Insert(width + gap, height + gap, heuristic);
                if (packed.Height <= 0)
                {
                    failed = true;
                    break;
                }

                placements.Add(new PlacedPiece(
                    piece.ArtworkIndex, piece.CopyIndex, packed.X, packed.Y, width, height, forceRotated));
            }

            if (!failed && placements.Count == basePieces.Count)
                return new Candidate(placements, requiredPerArtwork);
        }

        // When every artwork allows rotation, let MaxRects choose the orientation
        // independently for each copy instead of forcing one orientation per type.
        if (request.Artworks.All(a => a.AllowRotation))
        {
            foreach (var ordering in orderings)
            foreach (var heuristic in Heuristics)
            {
                if (candidateTimer.Elapsed.TotalSeconds >= seconds ||
                    totalTimer.Elapsed.TotalSeconds >= Math.Clamp(request.SolverTimeLimitSeconds, 1, 300))
                    return null;

                var packer = new MaxRectsBinPack(usableWidth + gap, usableHeight + gap, allowRotations: true);
                var placements = new List<PlacedPiece>(ordering.Count);
                var failed = false;
                foreach (var piece in ordering)
                {
                    var packed = packer.Insert(piece.Width + gap, piece.Height + gap, heuristic);
                    if (packed.Height <= 0) { failed = true; break; }
                    var rotated = packed.Width == piece.Height + gap && packed.Height == piece.Width + gap;
                    var placedWidth = rotated ? piece.Height : piece.Width;
                    var placedHeight = rotated ? piece.Width : piece.Height;
                    placements.Add(new PlacedPiece(piece.ArtworkIndex, piece.CopyIndex, packed.X, packed.Y, placedWidth, placedHeight, rotated));
                }
                if (!failed && placements.Count == basePieces.Count)
                    return new Candidate(placements, requiredPerArtwork);
            }
        }

        return null;
    }

    private static List<bool[]> BuildOrientationPlans(IReadOnlyList<ImpositionArtwork> artworks)
    {
        var plans = new List<bool[]>();
        var rotatable = Enumerable.Range(0, artworks.Count)
            .Where(i => artworks[i].AllowRotation && artworks[i].WidthMm != artworks[i].HeightMm)
            .ToArray();

        // Always test all-native orientation.
        plans.Add(new bool[artworks.Count]);
        if (rotatable.Length == 0)
            return plans;

        if (rotatable.Length <= MaxArtworkTypesForFullOrientationSweep)
        {
            var combinations = 1 << rotatable.Length;
            for (var mask = 1; mask < combinations; mask++)
            {
                var plan = new bool[artworks.Count];
                for (var bit = 0; bit < rotatable.Length; bit++)
                    plan[rotatable[bit]] = (mask & (1 << bit)) != 0;
                plans.Add(plan);
            }
        }
        else
        {
            foreach (var index in rotatable)
            {
                var plan = new bool[artworks.Count];
                plan[index] = true;
                plans.Add(plan);
            }
            var allRotated = new bool[artworks.Count];
            foreach (var index in rotatable)
                allRotated[index] = true;
            plans.Add(allRotated);
        }

        return plans;
    }

    private static List<List<Piece>> BuildOrderings(List<Piece> pieces)
    {
        var orders = new List<List<Piece>>();
        void Add(IEnumerable<Piece> ordered)
        {
            var list = ordered.ToList();
            if (!orders.Any(existing => existing.Select(Key).SequenceEqual(list.Select(Key))))
                orders.Add(list);
        }

        Add(pieces.OrderByDescending(p => (long)p.Width * p.Height)
            .ThenByDescending(p => Math.Max(p.Width, p.Height))
            .ThenByDescending(p => Math.Min(p.Width, p.Height)));
        Add(pieces.OrderByDescending(p => Math.Max(p.Width, p.Height))
            .ThenByDescending(p => (long)p.Width * p.Height));
        Add(pieces.OrderByDescending(p => p.Height).ThenByDescending(p => p.Width));
        Add(pieces.OrderByDescending(p => p.Width).ThenByDescending(p => p.Height));
        Add(pieces.OrderBy(p => (long)p.Width * p.Height)
            .ThenBy(p => Math.Max(p.Width, p.Height)));
        return orders;

        static string Key(Piece p) => $"{p.ArtworkIndex}:{p.CopyIndex}";
    }

    private static int FindAreaLowerBound(
        MixedImpositionRequest request, int usableWidth, int usableHeight, int gap, int maxRepeats)
    {
        var availableArea = (long)(usableWidth + gap) * (usableHeight + gap);
        var low = 1;
        var high = maxRepeats;
        while (low < high)
        {
            var mid = low + (high - low) / 2;
            long requiredArea = 0;
            for (var i = 0; i < request.Artworks.Count; i++)
            {
                var artwork = request.Artworks[i];
                var copies = Math.Max(Math.Max(1, artwork.MinimumPerRepeat), DivideRoundUp(artwork.Quantity, mid));
                var w = ToUnits(artwork.WidthMm) + gap;
                var h = ToUnits(artwork.HeightMm) + gap;
                requiredArea += (long)copies * w * h;
            }

            if (requiredArea <= availableArea)
                high = mid;
            else
                low = mid + 1;
        }
        return low;
    }

    private static ImpositionAlternative BuildResult(
        MixedImpositionRequest request, Candidate candidate, int repeats, bool ascendingSearchFoundCandidate)
    {
        var placements = candidate.Placements.Select((piece, index) => new ImpositionPlacement(
            index + 1,
            request.MarginMm + FromUnits(piece.X),
            request.MarginMm + FromUnits(piece.Y),
            FromUnits(piece.Width),
            FromUnits(piece.Height),
            request.Artworks[piece.ArtworkIndex].Name,
            piece.ArtworkIndex,
            piece.Rotated,
            null)).ToList();

        var perRepeat = request.Artworks.Select((artwork, index) =>
                new KeyValuePair<string, int>(artwork.Name, candidate.RequiredPerArtwork[index]))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        var planned = perRepeat.ToDictionary(x => x.Key, x => checked(x.Value * repeats), StringComparer.Ordinal);
        var stockAreaPerRepeat = request.StockWidthMm * request.StockHeightMm;
        var usedAreaPerRepeat = placements.Sum(p => p.Width * p.Height);
        var stockArea = stockAreaPerRepeat * repeats;
        var usedArea = usedAreaPerRepeat * repeats;
        var waste = Math.Max(0m, stockArea - usedArea);

        return new ImpositionAlternative(
            $"MaxRects mixed layout — {repeats} repeat(s)",
            request.IsRoll ? "Roll" : "Sheet",
            placements.Any(p => p.Rotated),
            0,
            0,
            placements.Count,
            repeats,
            planned.Values.Sum(),
            stockArea,
            request.StockWidthMm,
            request.StockHeightMm,
            usedArea,
            waste,
            stockArea <= 0 ? 100m : Math.Round(waste / stockArea * 100m, 2),
            stockArea <= 0 ? 0m : Math.Round(usedArea / stockArea * 100m, 2),
            request.IsRoll ? request.StockHeightMm * repeats : 0m,
            placements,
            perRepeat,
            ascendingSearchFoundCandidate
                ? "MaxRects heuristic — first feasible result in ascending repeat-count search; global optimum is not proven"
                : "MaxRects heuristic — feasible fallback layout found; lower repeat counts were not fully searched",
            planned);
    }

    private static void ValidateSolvedLayout(MixedImpositionRequest request, ImpositionAlternative result)
    {
        const decimal epsilon = 0.0001m;
        var left = request.MarginMm;
        var top = request.MarginMm;
        var right = request.StockWidthMm - request.MarginMm;
        var bottom = request.StockHeightMm - request.MarginMm;

        foreach (var p in result.Placements)
        {
            if (p.X < left - epsilon || p.Y < top - epsilon ||
                p.X + p.Width > right + epsilon || p.Y + p.Height > bottom + epsilon)
                throw new InvalidOperationException($"MaxRects validation failed: '{p.ArtworkName}' is outside the usable stock area.");
        }

        for (var i = 0; i < result.Placements.Count; i++)
        for (var j = i + 1; j < result.Placements.Count; j++)
        {
            var a = result.Placements[i];
            var b = result.Placements[j];
            var separated =
                a.X + a.Width + request.GapMm <= b.X + epsilon ||
                b.X + b.Width + request.GapMm <= a.X + epsilon ||
                a.Y + a.Height + request.GapMm <= b.Y + epsilon ||
                b.Y + b.Height + request.GapMm <= a.Y + epsilon;
            if (!separated)
                throw new InvalidOperationException($"MaxRects validation failed: '{a.ArtworkName}' and '{b.ArtworkName}' overlap or violate the gap.");
        }

        foreach (var artwork in request.Artworks)
        {
            var perRepeat = result.PiecesPerArtwork?.GetValueOrDefault(artwork.Name) ?? 0;
            var planned = result.PlannedQuantityPerArtwork?.GetValueOrDefault(artwork.Name) ?? 0;
            if (perRepeat < Math.Max(1, artwork.MinimumPerRepeat) || planned < artwork.Quantity)
                throw new InvalidOperationException($"MaxRects validation failed: '{artwork.Name}' does not meet its requested quantity.");
        }
    }

    private static void ValidateIndividualFit(MixedImpositionRequest request, int usableWidth, int usableHeight)
    {
        foreach (var artwork in request.Artworks)
        {
            var w = ToUnits(artwork.WidthMm);
            var h = ToUnits(artwork.HeightMm);
            var normalFits = w <= usableWidth && h <= usableHeight;
            var rotatedFits = artwork.AllowRotation && h <= usableWidth && w <= usableHeight;
            if (!normalFits && !rotatedFits)
                throw new ArgumentException(
                    $"'{artwork.Name}' ({artwork.WidthMm:0.##} × {artwork.HeightMm:0.##} mm) cannot fit inside the usable stock ({FromUnits(usableWidth):0.##} × {FromUnits(usableHeight):0.##} mm). " +
                    (artwork.AllowRotation ? "Neither orientation fits." : "Rotation is disabled."));
        }
    }

    private static void ValidateRequest(MixedImpositionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Artworks is null || request.Artworks.Count == 0)
            throw new ArgumentException("Add at least one artwork before optimizing.");
        if (request.StockWidthMm <= 0 || request.StockHeightMm <= 0 ||
            request.MarginMm < 0 || request.GapMm < 0 ||
            request.StockWidthMm <= 2m * request.MarginMm ||
            request.StockHeightMm <= 2m * request.MarginMm)
            throw new ArgumentException("Enter valid stock dimensions, margins and gaps.");
        if (request.StockWidthMm > 4000m || request.StockHeightMm > 4000m ||
            request.StockWidthMm - 2m * request.MarginMm + request.GapMm > 4600m ||
            request.StockHeightMm - 2m * request.MarginMm + request.GapMm > 4600m)
            throw new ArgumentException("This MaxRects implementation supports usable stock dimensions up to 4,600 mm per side.");
        if (request.Artworks.Any(a => string.IsNullOrWhiteSpace(a.Name) || a.WidthMm <= 0 ||
            a.HeightMm <= 0 || a.Quantity <= 0 || a.MinimumPerRepeat < 0 || a.MinimumPerRepeat > a.Quantity))
            throw new ArgumentException("Every artwork needs a name, positive dimensions and quantity, and a valid minimum-per-repeat value.");
        if (request.Artworks.Select(a => a.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Artworks.Count)
            throw new ArgumentException("Artwork names must be unique. Give each artwork type a different name.");
    }

    private static int DivideRoundUp(int value, int divisor) => (value + divisor - 1) / divisor;
    private static int ToUnits(decimal mm) => checked((int)Math.Round(mm * UnitsPerMm, MidpointRounding.AwayFromZero));
    private static decimal FromUnits(int value) => value / (decimal)UnitsPerMm;

    private sealed record Piece(int ArtworkIndex, int CopyIndex, int Width, int Height);
    private sealed record PlacedPiece(int ArtworkIndex, int CopyIndex, int X, int Y, int Width, int Height, bool Rotated);
    private sealed record Candidate(List<PlacedPiece> Placements, int[] RequiredPerArtwork);
}
