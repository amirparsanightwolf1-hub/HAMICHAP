namespace HamiChap_MES_ERP.Models.Imposition;

public enum ArtworkVectorSource
{
    ExistingPdfVectorPaths,
    TracedFromRasterRender
}

public sealed record ArtworkVectorizationResult(
    bool Success,
    ArtworkVectorSource? Source,
    int PageNumber,
    int DetectedPdfPaths,
    int ClosedPdfContours,
    int GeneratedContours,
    double PageWidthPoints,
    double PageHeightPoints,
    string? SvgFilePath,
    bool NeedsReview,
    string Message,
    IReadOnlyList<string> Warnings);

public sealed record ArtworkVectorizationOptions
{
    /// <summary>Page number is one-based.</summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>PDF raster fallback resolution. Larger values are more detailed but slower.</summary>
    public int RasterMaxDimension { get; init; } = 1600;

    /// <summary>Pixels whose grayscale luminance is below this value are traced as foreground.</summary>
    public byte RasterThreshold { get; init; } = 190;

    /// <summary>Ignore tiny raster contours below this pixel area.</summary>
    public int MinimumRasterContourAreaPixels { get; init; } = 8;

    /// <summary>Prevents pathological SVG output from noisy scans.</summary>
    public int MaximumRasterEdges { get; init; } = 400_000;
}
