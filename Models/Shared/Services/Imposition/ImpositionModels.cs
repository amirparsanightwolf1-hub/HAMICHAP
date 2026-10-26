namespace HamiChap_MES_ERP.Services.Imposition;

public sealed record ImpositionRequest(
    decimal ArtworkWidthMm,
    decimal ArtworkHeightMm,
    decimal StockWidthMm,
    decimal StockHeightMm,
    decimal MarginMm,
    decimal GapMm,
    int RequiredQuantity,
    bool AllowRotation = true);

public sealed record ImpositionAlternative(
    string Name,
    string Mode,
    bool Rotated,
    int Across,
    int Down,
    int PiecesPerSheet,
    int SheetsRequired,
    int ProducedQuantity,
    decimal StockAreaMm2,
    decimal StockWidthMm,
    decimal StockHeightMm,
    decimal UsedAreaMm2,
    decimal WasteAreaMm2,
    decimal WastePercent,
    decimal UtilizationPercent,
    decimal RollLengthMm,
    IReadOnlyList<ImpositionPlacement> Placements,
    IReadOnlyDictionary<string, int>? PiecesPerArtwork = null,
    string SolverStatus = "Heuristic",
    IReadOnlyDictionary<string, int>? PlannedQuantityPerArtwork = null);

public sealed record ImpositionPlacement(int Index, decimal X, decimal Y, decimal Width, decimal Height, string ArtworkName = "", int ArtworkIndex = 0);

public sealed record ImpositionArtwork(string Name, decimal WidthMm, decimal HeightMm, int Quantity, bool AllowRotation = true, int MinimumPerRepeat = 0);

public sealed record MixedImpositionRequest(
    IReadOnlyList<ImpositionArtwork> Artworks,
    decimal StockWidthMm,
    decimal StockHeightMm,
    decimal MarginMm,
    decimal GapMm,
    bool IsRoll,
    int SolverTimeLimitSeconds = 30);


public sealed class ImpositionOptimizerService
{
    public IReadOnlyList<ImpositionAlternative> OptimizeSheet(ImpositionRequest r)
    {
        Validate(r, requireHeight: true);
        var options = new List<ImpositionAlternative>();
        AddSheetOption(r, false, options);
        if (r.AllowRotation && r.ArtworkWidthMm != r.ArtworkHeightMm) AddSheetOption(r, true, options);
        return options.OrderByDescending(x => x.ProducedQuantity == 0 ? 0 : Math.Min(x.ProducedQuantity, r.RequiredQuantity))
            .ThenBy(x => x.WastePercent).ThenBy(x => x.SheetsRequired).ToList();
    }

    public IReadOnlyList<ImpositionAlternative> OptimizeRoll(ImpositionRequest r)
    {
        Validate(r, requireHeight: false);
        var options = new List<ImpositionAlternative>();
        AddRollOption(r, false, options);
        if (r.AllowRotation && r.ArtworkWidthMm != r.ArtworkHeightMm) AddRollOption(r, true, options);
        return options.OrderBy(x => x.RollLengthMm).ThenBy(x => x.WastePercent).ToList();
    }

    public IReadOnlyList<ImpositionAlternative> OptimizeMixed(MixedImpositionRequest request)
    {
        ValidateMixed(request);
        return ExactMixedImpositionSolver.Solve(request);
    }

    private static ImpositionAlternative PackMixed(MixedImpositionRequest request, List<(ImpositionArtwork Artwork, int Index)> order, string name)
    {
        var usableW = request.StockWidthMm - 2 * request.MarginMm;
        var usableH = request.IsRoll ? decimal.MaxValue : request.StockHeightMm - 2 * request.MarginMm;
        var remaining = request.Artworks.Select(a => a.Quantity).ToArray();
        var placements = new List<ImpositionPlacement>();
        var firstUnitPlacements = new List<ImpositionPlacement>();
        var produced = 0;
        var sheetCount = 0;
        decimal lastY = request.MarginMm;
        decimal usedAreaTotal = 0;
        const decimal eps = 0.0001m;

        // Pack each sheet independently (or the full roll) by repeatedly choosing the
        // piece that best uses the remaining row width. This deliberately does NOT force
        // one copy of every artwork into a sheet; high-count/small pieces can occupy most
        // of a sheet, subject to each artwork's requested quantity.
        while (remaining.Any(q => q > 0))
        {
            var sheetIndex = sheetCount;
            sheetCount++;
            var y = request.MarginMm;
            var sheetHasPieces = false;

            while (remaining.Any(q => q > 0))
            {
                if (!request.IsRoll && y >= request.StockHeightMm - request.MarginMm - eps) break;

                var rowCandidates = new List<(int Index, decimal W, decimal H, int FitCount, decimal Fill, decimal Score)>();
                for (var i = 0; i < request.Artworks.Count; i++)
                {
                    if (remaining[i] <= 0) continue;
                    var a = request.Artworks[i];
                    AddOrientation(i, a.WidthMm, a.HeightMm);
                    if (a.AllowRotation && a.WidthMm != a.HeightMm) AddOrientation(i, a.HeightMm, a.WidthMm);

                    void AddOrientation(int idx, decimal w, decimal h)
                    {
                        if (w > usableW || (!request.IsRoll && h > usableH)) return;
                        var fit = CountFit(usableW, w, request.GapMm);
                        if (fit <= 0) return;
                        var actual = Math.Min(remaining[idx], fit);
                        var fill = actual * w * h;
                        // Favor the orientation that puts the most pieces into a row;
                        // break ties by area use, then shorter row height.
                        var score = actual * 1000000000m + fill * 1000m - h;
                        rowCandidates.Add((idx, w, h, (int)actual, fill, score));
                    }
                }

                if (rowCandidates.Count == 0) break;
                var best = rowCandidates.OrderByDescending(c => c.Score).First();
                var rowH = best.H;
                var x = request.MarginMm;
                var rowPlacements = new List<ImpositionPlacement>();

                // Fill the row with whichever remaining artwork fits next. At each step,
                // choose the piece giving the highest piece count / area use in the space left.
                while (remaining.Any(q => q > 0) && x + 0.0001m <= request.StockWidthMm - request.MarginMm)
                {
                    var availableW = request.StockWidthMm - request.MarginMm - x;
                    var choices = new List<(int Index, decimal W, decimal H, decimal Score)>();
                    for (var i = 0; i < request.Artworks.Count; i++)
                    {
                        if (remaining[i] <= 0) continue;
                        var a = request.Artworks[i];
                        AddChoice(i, a.WidthMm, a.HeightMm);
                        if (a.AllowRotation && a.WidthMm != a.HeightMm) AddChoice(i, a.HeightMm, a.WidthMm);

                        void AddChoice(int idx, decimal w, decimal h)
                        {
                            if (w > availableW + eps || (!request.IsRoll && y + h > request.StockHeightMm - request.MarginMm + eps)) return;
                            if (h > rowH + eps) return; // keep this row at its selected height
                            var fit = CountFit(availableW, w, request.GapMm);
                            var count = Math.Min(remaining[idx], Math.Max(1, fit));
                            var score = count * 1000000000m + count * w * h * 1000m - h;
                            choices.Add((idx, w, h, score));
                        }
                    }
                    if (choices.Count == 0) break;
                    var choice = choices.OrderByDescending(c => c.Score).First();
                    var p = new ImpositionPlacement(produced + rowPlacements.Count + 1, x, y, choice.W, choice.H,
                        request.Artworks[choice.Index].Name, choice.Index);
                    rowPlacements.Add(p);
                    remaining[choice.Index]--;
                    x += choice.W + request.GapMm;
                    rowH = Math.Max(rowH, choice.H);
                }

                if (rowPlacements.Count == 0) break;
                sheetHasPieces = true;
                foreach (var p in rowPlacements)
                {
                    produced++;
                    usedAreaTotal += p.Width * p.Height;
                    lastY = Math.Max(lastY, p.Y + p.Height);
                    // Keep the first sheet for sheet preview; for roll preview keep only
                    // the first repeat row so the UI never renders the full roll length.
                    if (request.IsRoll)
                    {
                        if (p.Y <= request.MarginMm + eps) firstUnitPlacements.Add(p);
                    }
                    else if (sheetIndex == 0)
                    {
                        firstUnitPlacements.Add(p);
                    }
                }

                y += rowH + request.GapMm;
                if (request.IsRoll) break; // next row extends the roll; keep iterating below
                if (y + eps >= request.StockHeightMm - request.MarginMm) break;
            }

            if (!sheetHasPieces)
            {
                // No remaining artwork fits the stock; stop instead of looping forever.
                break;
            }

            if (request.IsRoll)
            {
                // A roll is one continuous substrate: continue laying rows until all pieces
                // are placed, rather than treating each row as a separate sheet.
                // The outer loop is converted to a single continuous layout below.
                break;
            }
        }

        // Re-pack rolls as a continuous web. Sheet layouts above use per-sheet coordinates.
        if (request.IsRoll)
        {
            Array.Copy(request.Artworks.Select(a => a.Quantity).ToArray(), remaining, remaining.Length);
            placements.Clear(); firstUnitPlacements.Clear(); produced = 0; usedAreaTotal = 0;
            var y = request.MarginMm;
            while (remaining.Any(q => q > 0))
            {
                var x = request.MarginMm;
                var rowH = 0m;
                var row = new List<ImpositionPlacement>();
                while (remaining.Any(q => q > 0))
                {
                    var availableW = request.StockWidthMm - request.MarginMm - x;
                    if (availableW <= 0) break;
                    var choices = new List<(int Index, decimal W, decimal H, decimal Score)>();
                    for (var i = 0; i < request.Artworks.Count; i++)
                    {
                        if (remaining[i] <= 0) continue;
                        var a = request.Artworks[i]; AddChoice(i, a.WidthMm, a.HeightMm);
                        if (a.AllowRotation && a.WidthMm != a.HeightMm) AddChoice(i, a.HeightMm, a.WidthMm);
                        void AddChoice(int idx, decimal w, decimal h)
                        {
                            if (w > availableW + eps || w > usableW) return;
                            var fit = CountFit(availableW, w, request.GapMm);
                            var count = Math.Min(remaining[idx], Math.Max(1, fit));
                            choices.Add((idx, w, h, count * 1000000000m + count * w * h * 1000m - h));
                        }
                    }
                    if (choices.Count == 0) break;
                    var c = choices.OrderByDescending(v => v.Score).First();
                    row.Add(new(produced + row.Count + 1, x, y, c.W, c.H, request.Artworks[c.Index].Name, c.Index));
                    remaining[c.Index]--; x += c.W + request.GapMm; rowH = Math.Max(rowH, c.H);
                }
                if (row.Count == 0) break;
                foreach (var p in row) { placements.Add(p); produced++; usedAreaTotal += p.Width * p.Height; lastY = Math.Max(lastY, p.Y + p.Height); }
                if (firstUnitPlacements.Count == 0) firstUnitPlacements.AddRange(row);
                y += rowH + request.GapMm;
            }
            sheetCount = produced == 0 ? 0 : 1;
        }

        var rollLength = request.IsRoll ? (produced == 0 ? 0 : lastY + request.MarginMm) : 0;
        var stockArea = request.IsRoll
            ? request.StockWidthMm * rollLength
            : request.StockWidthMm * request.StockHeightMm * sheetCount;
        var waste = Math.Max(0, stockArea - usedAreaTotal);
        var wastePct = stockArea == 0 ? 100 : Math.Round(waste / stockArea * 100, 2);
        var util = stockArea == 0 ? 0 : Math.Round(usedAreaTotal / stockArea * 100, 2);
        return new(name, request.IsRoll ? "Roll" : "Sheet", false, 0, 0, produced, sheetCount, produced,
            stockArea, request.StockWidthMm, request.IsRoll ? Math.Max(1, rollLength) : request.StockHeightMm,
            usedAreaTotal, waste, wastePct, util, rollLength, firstUnitPlacements);
    }

    private static void ValidateMixed(MixedImpositionRequest r)
    {
        if (r.Artworks == null || r.Artworks.Count == 0) throw new ArgumentException("Add at least one artwork.");
        if (r.StockWidthMm <= 0 || (!r.IsRoll && r.StockHeightMm <= 0) || r.MarginMm < 0 || r.GapMm < 0 || r.StockWidthMm <= 2 * r.MarginMm || (!r.IsRoll && r.StockHeightMm <= 2 * r.MarginMm))
            throw new ArgumentException("Stock dimensions must be positive and margins must leave usable stock area.");
        if (r.Artworks.Any(a => string.IsNullOrWhiteSpace(a.Name) || a.WidthMm <= 0 || a.HeightMm <= 0 || a.Quantity <= 0 || a.MinimumPerRepeat < 0 || a.MinimumPerRepeat > a.Quantity))
            throw new ArgumentException("Every artwork needs a name, positive dimensions, a positive quantity, and a minimum per repeat between zero and its required quantity.");
        if (r.Artworks.Select(a => a.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != r.Artworks.Count)
            throw new ArgumentException("Each artwork must have a unique name so per-artwork production counts can be reported separately.");
    }

    private static void AddSheetOption(ImpositionRequest r, bool rotated, List<ImpositionAlternative> output)
    {
        var w = rotated ? r.ArtworkHeightMm : r.ArtworkWidthMm;
        var h = rotated ? r.ArtworkWidthMm : r.ArtworkHeightMm;
        var usableW = r.StockWidthMm - 2 * r.MarginMm;
        var usableH = r.StockHeightMm - 2 * r.MarginMm;
        var across = CountFit(usableW, w, r.GapMm);
        var down = CountFit(usableH, h, r.GapMm);
        var perSheet = across * down;
        var sheets = perSheet == 0 ? 0 : (int)Math.Ceiling((double)r.RequiredQuantity / perSheet);
        var produced = perSheet * sheets;
        var stockArea = r.StockWidthMm * r.StockHeightMm;
        var used = perSheet * w * h;
        var waste = Math.Max(0, stockArea - used);
        var placements = new List<ImpositionPlacement>();
        for (var y = 0; y < down; y++) for (var x = 0; x < across; x++)
            placements.Add(new(placements.Count + 1, r.MarginMm + x * (w + r.GapMm), r.MarginMm + y * (h + r.GapMm), w, h));
        output.Add(new(rotated ? "Sheet — rotated" : "Sheet — original orientation", "Sheet", rotated, across, down, perSheet, sheets, produced, stockArea, r.StockWidthMm, r.StockHeightMm, used, waste, stockArea == 0 ? 100 : Math.Round(waste / stockArea * 100, 2), stockArea == 0 ? 0 : Math.Round(used / stockArea * 100, 2), 0, placements));
    }

    private static void AddRollOption(ImpositionRequest r, bool rotated, List<ImpositionAlternative> output)
    {
        var w = rotated ? r.ArtworkHeightMm : r.ArtworkWidthMm;
        var h = rotated ? r.ArtworkWidthMm : r.ArtworkHeightMm;
        var usableW = r.StockWidthMm - 2 * r.MarginMm;
        var across = CountFit(usableW, w, r.GapMm);
        var repeats = across == 0 ? 0 : (int)Math.Ceiling((double)r.RequiredQuantity / across);
        var length = repeats == 0 ? 0 : 2 * r.MarginMm + repeats * h + Math.Max(0, repeats - 1) * r.GapMm;
        var stockArea = r.StockWidthMm * length;
        var used = Math.Min(r.RequiredQuantity, across * repeats) * w * h;
        var waste = Math.Max(0, stockArea - used);
        var placements = new List<ImpositionPlacement>();
        for (var y = 0; y < repeats; y++) for (var x = 0; x < across && placements.Count < r.RequiredQuantity; x++)
            placements.Add(new(placements.Count + 1, r.MarginMm + x * (w + r.GapMm), r.MarginMm + y * (h + r.GapMm), w, h));
        output.Add(new(rotated ? "Roll — rotated" : "Roll — original orientation", "Roll", rotated, across, repeats, across, repeats, placements.Count, stockArea, r.StockWidthMm, length, used, waste, stockArea == 0 ? 100 : Math.Round(waste / stockArea * 100, 2), stockArea == 0 ? 0 : Math.Round(used / stockArea * 100, 2), length, placements));
    }

    private static int CountFit(decimal usable, decimal item, decimal gap) => usable < item || item <= 0 ? 0 : (int)Math.Floor((double)((usable + gap) / (item + gap)));

    private static void Validate(ImpositionRequest r, bool requireHeight)
    {
        if (r.ArtworkWidthMm <= 0 || r.ArtworkHeightMm <= 0 || r.StockWidthMm <= 0 || r.MarginMm < 0 || r.GapMm < 0 || r.RequiredQuantity <= 0)
            throw new ArgumentException("Dimensions and quantity must be positive; margins and gaps cannot be negative.");
        if (requireHeight && r.StockHeightMm <= 0) throw new ArgumentException("Sheet height must be greater than zero.");
        if (r.StockWidthMm <= 2 * r.MarginMm || (requireHeight && r.StockHeightMm <= 2 * r.MarginMm))
            throw new ArgumentException("Margins leave no usable stock area.");
    }
}
