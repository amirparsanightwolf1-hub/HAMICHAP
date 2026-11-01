namespace HamiChap_MES_ERP.Models.Imposition;

/// <summary>Normalized geometry for downstream fitting. Dimensions are millimetres.</summary>
public sealed record StandardArtworkGeometryResult(
    bool Success,
    string? AllContoursSvgPath,
    string? PrimaryContourSvgPath,
    decimal WidthMm,
    decimal HeightMm,
    int CandidateContourCount,
    int PrimaryContourIndex,
    string CoordinateUnit,
    bool NeedsReview,
    string Message,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Degrees applied to align the longest detected contour segment with the horizontal axis.</summary>
    public decimal AppliedRotationDegrees { get; init; }
}
