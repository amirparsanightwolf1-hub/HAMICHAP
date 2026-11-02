namespace HamiChap_MES_ERP.Models.Imposition;

/// <summary>Conservative axis-aligned rectangular cover of a single artwork contour, in millimetres.</summary>
public sealed record ArtworkOccupiedRectangle(decimal Xmm, decimal Ymm, decimal WidthMm, decimal HeightMm);

public sealed record ArtworkRectangleDecompositionResult(
    bool Success,
    decimal ArtworkWidthMm,
    decimal ArtworkHeightMm,
    decimal GridResolutionMm,
    IReadOnlyList<ArtworkOccupiedRectangle> Rectangles,
    string? PreviewSvgPath,
    bool NeedsReview,
    string Message,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Path to the SVG compound path representing the geometric union of all generated rectangles.</summary>
    public string? MergedShapeSvgPath { get; init; }
}
