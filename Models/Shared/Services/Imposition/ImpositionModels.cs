using HamiChap_MES_ERP.Models.Imposition;

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

public sealed record ImpositionPlacement(
    int Index,
    decimal X,
    decimal Y,
    decimal Width,
    decimal Height,
    string ArtworkName = "",
    int ArtworkIndex = 0,
    bool Rotated = false,
    IReadOnlyList<ArtworkOccupiedRectangle>? OccupiedRectangles = null);

public sealed record ImpositionArtwork(
    string Name,
    decimal WidthMm,
    decimal HeightMm,
    int Quantity,
    bool AllowRotation = true,
    int MinimumPerRepeat = 0,
    IReadOnlyList<ArtworkOccupiedRectangle>? OccupiedRectangles = null);

public sealed record MixedImpositionRequest(
    IReadOnlyList<ImpositionArtwork> Artworks,
    decimal StockWidthMm,
    decimal StockHeightMm,
    decimal MarginMm,
    decimal GapMm,
    bool IsRoll,
    int SolverTimeLimitSeconds = 30);

/// <summary>
/// Application entry point for imposition calculations.
/// Mixed artwork uses the MaxRects rectangular packing engine.
/// </summary>
public sealed class ImpositionOptimizerService
{
    public IReadOnlyList<ImpositionAlternative> OptimizeSheet(ImpositionRequest request)
    {
        ValidateSingle(request, requireHeight: true);

        var alternatives = new List<ImpositionAlternative>();
        AddSheetOption(request, rotated: false, alternatives);
        if (request.AllowRotation && request.ArtworkWidthMm != request.ArtworkHeightMm)
            AddSheetOption(request, rotated: true, alternatives);

        return alternatives
            .OrderByDescending(x => Math.Min(x.ProducedQuantity, request.RequiredQuantity))
            .ThenBy(x => x.WastePercent)
            .ThenBy(x => x.SheetsRequired)
            .ToList();
    }

    public IReadOnlyList<ImpositionAlternative> OptimizeRoll(ImpositionRequest request)
    {
        ValidateSingle(request, requireHeight: false);

        var alternatives = new List<ImpositionAlternative>();
        AddRollOption(request, rotated: false, alternatives);
        if (request.AllowRotation && request.ArtworkWidthMm != request.ArtworkHeightMm)
            AddRollOption(request, rotated: true, alternatives);

        return alternatives
            .OrderBy(x => x.RollLengthMm)
            .ThenBy(x => x.WastePercent)
            .ToList();
    }

    public IReadOnlyList<ImpositionAlternative> OptimizeMixed(MixedImpositionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return MaxRectsMixedImpositionSolver.Solve(request);
    }

    private static void AddSheetOption(
        ImpositionRequest request,
        bool rotated,
        List<ImpositionAlternative> output)
    {
        var width = rotated ? request.ArtworkHeightMm : request.ArtworkWidthMm;
        var height = rotated ? request.ArtworkWidthMm : request.ArtworkHeightMm;
        var usableWidth = request.StockWidthMm - 2m * request.MarginMm;
        var usableHeight = request.StockHeightMm - 2m * request.MarginMm;
        var across = CountFit(usableWidth, width, request.GapMm);
        var down = CountFit(usableHeight, height, request.GapMm);
        var perSheet = across * down;
        var sheets = perSheet == 0 ? 0 : (int)Math.Ceiling((double)request.RequiredQuantity / perSheet);
        var produced = perSheet * sheets;
        var stockArea = request.StockWidthMm * request.StockHeightMm;
        var usedArea = perSheet * width * height;
        var waste = Math.Max(0m, stockArea - usedArea);
        var placements = new List<ImpositionPlacement>();

        for (var row = 0; row < down; row++)
        for (var column = 0; column < across; column++)
        {
            placements.Add(new ImpositionPlacement(
                placements.Count + 1,
                request.MarginMm + column * (width + request.GapMm),
                request.MarginMm + row * (height + request.GapMm),
                width,
                height,
                "Artwork",
                0,
                rotated));
        }

        output.Add(new ImpositionAlternative(
            rotated ? "Sheet — rotated" : "Sheet — original orientation",
            "Sheet",
            rotated,
            across,
            down,
            perSheet,
            sheets,
            produced,
            stockArea,
            request.StockWidthMm,
            request.StockHeightMm,
            usedArea,
            waste,
            stockArea == 0 ? 100 : Math.Round(waste / stockArea * 100m, 2),
            stockArea == 0 ? 0 : Math.Round(usedArea / stockArea * 100m, 2),
            0,
            placements));
    }

    private static void AddRollOption(
        ImpositionRequest request,
        bool rotated,
        List<ImpositionAlternative> output)
    {
        var width = rotated ? request.ArtworkHeightMm : request.ArtworkWidthMm;
        var height = rotated ? request.ArtworkWidthMm : request.ArtworkHeightMm;
        var usableWidth = request.StockWidthMm - 2m * request.MarginMm;
        var across = CountFit(usableWidth, width, request.GapMm);
        var repeats = across == 0 ? 0 : (int)Math.Ceiling((double)request.RequiredQuantity / across);
        var length = repeats == 0
            ? 0
            : 2m * request.MarginMm + repeats * height + Math.Max(0, repeats - 1) * request.GapMm;
        var stockArea = request.StockWidthMm * length;
        var produced = Math.Min(request.RequiredQuantity, across * repeats);
        var usedArea = produced * width * height;
        var waste = Math.Max(0m, stockArea - usedArea);
        var placements = new List<ImpositionPlacement>();

        for (var row = 0; row < repeats; row++)
        for (var column = 0; column < across && placements.Count < request.RequiredQuantity; column++)
        {
            placements.Add(new ImpositionPlacement(
                placements.Count + 1,
                request.MarginMm + column * (width + request.GapMm),
                request.MarginMm + row * (height + request.GapMm),
                width,
                height,
                "Artwork",
                0,
                rotated));
        }

        output.Add(new ImpositionAlternative(
            rotated ? "Roll — rotated" : "Roll — original orientation",
            "Roll",
            rotated,
            across,
            repeats,
            across,
            repeats,
            produced,
            stockArea,
            request.StockWidthMm,
            length,
            usedArea,
            waste,
            stockArea == 0 ? 100 : Math.Round(waste / stockArea * 100m, 2),
            stockArea == 0 ? 0 : Math.Round(usedArea / stockArea * 100m, 2),
            length,
            placements));
    }

    private static int CountFit(decimal usable, decimal item, decimal gap) =>
        usable < item || item <= 0
            ? 0
            : (int)Math.Floor((double)((usable + gap) / (item + gap)));

    private static void ValidateSingle(ImpositionRequest request, bool requireHeight)
    {
        if (request.ArtworkWidthMm <= 0 ||
            request.ArtworkHeightMm <= 0 ||
            request.StockWidthMm <= 0 ||
            request.MarginMm < 0 ||
            request.GapMm < 0 ||
            request.RequiredQuantity <= 0)
        {
            throw new ArgumentException(
                "Artwork dimensions, stock width and quantity must be positive. Margins and gaps cannot be negative.");
        }

        if (requireHeight && request.StockHeightMm <= 0)
            throw new ArgumentException("Sheet height must be greater than zero.");

        if (request.StockWidthMm <= 2m * request.MarginMm ||
            (requireHeight && request.StockHeightMm <= 2m * request.MarginMm))
        {
            throw new ArgumentException("Margins leave no usable stock area.");
        }
    }
}
