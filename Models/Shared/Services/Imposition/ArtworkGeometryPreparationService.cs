using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HamiChap_MES_ERP.Models.Imposition;

namespace HamiChap_MES_ERP.Services.Imposition;

/// <summary>
/// Converts the vectorizer's SVG into a stable millimetre-based geometry asset for the
/// nesting engine. It keeps all paths for review and creates a second SVG containing the
/// largest closed path candidate. Largest path is only a heuristic, never a guaranteed cutline.
/// </summary>
public sealed class ArtworkGeometryPreparationService
{
    private static readonly XNamespace SvgNs = "http://www.w3.org/2000/svg";
    private static readonly Regex TokenRegex = new(@"[A-Za-z]|[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?", RegexOptions.Compiled);
    private static readonly Regex NumberRegex = new(@"[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?", RegexOptions.Compiled);

    public async Task<StandardArtworkGeometryResult> PrepareAsync(
        ArtworkVectorizationResult vectorization,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vectorization);
        if (!vectorization.Success || string.IsNullOrWhiteSpace(vectorization.SvgFilePath) || !File.Exists(vectorization.SvgFilePath))
            return new(false, null, null, 0, 0, 0, 0, "mm", true,
                "Vectorization did not produce an SVG file.", new[] { "Run the PDF vectorization step successfully first." });

        Directory.CreateDirectory(outputDirectory);
        var doc = XDocument.Load(vectorization.SvgFilePath, LoadOptions.None);
        var root = doc.Root;
        if (root is null || root.Name.LocalName != "svg")
            throw new InvalidDataException("The vectorizer output is not a valid SVG document.");

        var viewBox = ParseViewBox((string?)root.Attribute("viewBox"));
        if (viewBox.Width <= 0 || viewBox.Height <= 0)
            throw new InvalidDataException("The SVG has no valid viewBox.");

        // PDF-vector SVG uses points as viewBox units. Raster SVG uses pixels and maps the
        // viewBox to the physical PDF page dimensions. Both are converted to millimetres.
        var mmPerUnitX = vectorization.Source == ArtworkVectorSource.ExistingPdfVectorPaths
            ? 25.4 / 72.0
            : vectorization.PageWidthPoints * 25.4 / 72.0 / viewBox.Width;
        var mmPerUnitY = vectorization.Source == ArtworkVectorSource.ExistingPdfVectorPaths
            ? 25.4 / 72.0
            : vectorization.PageHeightPoints * 25.4 / 72.0 / viewBox.Height;

        var paths = root.Descendants().Where(e => e.Name.LocalName == "path")
            .Select((element, index) => new PathCandidate(index + 1, element, GetApproximateBounds((string?)element.Attribute("d"))))
            .Where(p => p.Bounds.IsValid)
            .ToList();

        if (paths.Count == 0)
            return new(false, null, null, 0, 0, 0, 0, "mm", true,
                "No SVG paths with usable geometry were found.", new[] { "Check the source PDF and rerun vectorization." });

        // The largest bounding-box area is a useful first candidate for the outer die-line,
        // but cannot reliably distinguish cut lines from a page frame or printed graphics.
        var primary = paths.OrderByDescending(p => p.Bounds.Width * p.Bounds.Height).First();
        var primaryBounds = primary.Bounds;
        var rotationDegrees = EstimateDeskewAngle((string?)primary.Element.Attribute("d"));

        // Apply the same rigid rotation to all review contours and the primary candidate so the
        // dominant straight edge is horizontal. Geometry is rotated, never stretched.
        var allBounds = paths.Select(p => p.Bounds).Aggregate((a, b) => a.Union(b));
        var rotatedAllBounds = RotateBounds(allBounds, rotationDegrees);
        var allPath = Path.Combine(outputDirectory, $"artwork-all-{Guid.NewGuid():N}.svg");
        var allSvg = CreateNormalizedSvg(paths.Select(p => p.Element), allBounds, rotatedAllBounds, mmPerUnitX, mmPerUnitY, rotationDegrees);
        await File.WriteAllTextAsync(allPath, allSvg, new UTF8Encoding(false), cancellationToken);

        var rotatedPrimaryBounds = RotateBounds(primaryBounds, rotationDegrees);
        var primaryPath = Path.Combine(outputDirectory, $"artwork-primary-{Guid.NewGuid():N}.svg");
        var primarySvg = CreateNormalizedSvg(new[] { primary.Element }, primaryBounds, rotatedPrimaryBounds, mmPerUnitX, mmPerUnitY, rotationDegrees);
        await File.WriteAllTextAsync(primaryPath, primarySvg, new UTF8Encoding(false), cancellationToken);

        var widthMm = (decimal)(rotatedPrimaryBounds.Width * mmPerUnitX);
        var heightMm = (decimal)(rotatedPrimaryBounds.Height * mmPerUnitY);
        return new StandardArtworkGeometryResult(true, allPath, primaryPath, Math.Round(widthMm, 3), Math.Round(heightMm, 3),
            paths.Count, primary.Index, "mm", true,
            $"Standardized SVG geometry generated in millimetres. Applied a {rotationDegrees:0.###}° alignment rotation to make the longest detected segment horizontal.",
            new[]
            {
                "Automatic rotation estimates the dominant edge from coordinate pairs in the primary SVG path. Curved paths or complex command sequences may produce an incorrect angle; review the preview.",
                "The primary contour is selected by bounding-box area, not semantic cut-line recognition. Review it before nesting.",
                "Path bounds are estimated from SVG path coordinate tokens. Curves may need accurate flattening before collision-safe nesting.",
                "This is not yet a certified manufacturing contour."
            }) { AppliedRotationDegrees = (decimal)rotationDegrees };
    }

    private static string CreateNormalizedSvg(IEnumerable<XElement> sourcePaths, Bounds sourceBounds, Bounds rotatedBounds,
        double mmPerUnitX, double mmPerUnitY, double rotationDegrees)
    {
        var width = Math.Max(0.001, rotatedBounds.Width * mmPerUnitX);
        var height = Math.Max(0.001, rotatedBounds.Height * mmPerUnitY);
        var cx = (sourceBounds.MinX + sourceBounds.MaxX) / 2.0;
        var cy = (sourceBounds.MinY + sourceBounds.MaxY) / 2.0;
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").Append(F(width)).Append("mm\" height=\"")
            .Append(F(height)).Append("mm\" viewBox=\"0 0 ").Append(F(width)).Append(' ').Append(F(height)).AppendLine("\">");
        // Nested transforms rotate the original vector geometry rigidly, translate the rotated
        // bounding box to (0,0), then scale to millimetres. The path commands remain vector paths.
        sb.Append("<g transform=\"scale(").Append(F(mmPerUnitX)).Append(' ').Append(F(mmPerUnitY)).AppendLine(")\">");
        sb.Append("<g transform=\"translate(").Append(F(-rotatedBounds.MinX)).Append(' ').Append(F(-rotatedBounds.MinY)).AppendLine(")\">");
        sb.Append("<g transform=\"rotate(").Append(F(-rotationDegrees)).Append(' ').Append(F(cx)).Append(' ').Append(F(cy)).AppendLine(")\">");
        foreach (var path in sourcePaths)
        {
            var clean = new XElement(path);
            clean.Attribute("data-source")?.Remove();
            sb.AppendLine(clean.ToString(SaveOptions.DisableFormatting));
        }
        sb.AppendLine("</g></g></g></svg>");
        return sb.ToString();
    }

    private static double EstimateDeskewAngle(string? d)
    {
        if (string.IsNullOrWhiteSpace(d)) return 0;
        var tokens = TokenRegex.Matches(d).Select(m => m.Value).ToArray();
        var numbers = tokens.Where(t => char.IsDigit(t[0]) || t[0] is '+' or '-' or '.')
            .Select(t => double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? (double?)n : null)
            .Where(n => n.HasValue).Select(n => n!.Value).ToArray();
        if (numbers.Length < 4) return 0;
        var points = new List<(double X, double Y)>();
        for (var i = 0; i + 1 < numbers.Length; i += 2) points.Add((numbers[i], numbers[i + 1]));
        double bestLength = 0, bestAngle = 0;
        for (var i = 1; i < points.Count; i++)
        {
            var dx = points[i].X - points[i - 1].X;
            var dy = points[i].Y - points[i - 1].Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length <= bestLength) continue;
            var angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            while (angle > 90) angle -= 180;
            while (angle < -90) angle += 180;
            bestLength = length;
            bestAngle = angle;
        }
        // Ignore tiny deviations likely caused by raster tracing noise.
        return Math.Abs(bestAngle) < 0.25 ? 0 : bestAngle;
    }

    private static Bounds RotateBounds(Bounds b, double angleDegrees)
    {
        if (Math.Abs(angleDegrees) < 0.000001) return b;
        var radians = -angleDegrees * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var cx = (b.MinX + b.MaxX) / 2.0;
        var cy = (b.MinY + b.MaxY) / 2.0;
        var corners = new[] { (b.MinX, b.MinY), (b.MaxX, b.MinY), (b.MaxX, b.MaxY), (b.MinX, b.MaxY) }
            .Select(p => (X: cx + (p.Item1 - cx) * cos - (p.Item2 - cy) * sin,
                          Y: cy + (p.Item1 - cx) * sin + (p.Item2 - cy) * cos)).ToArray();
        return new(corners.Min(p => p.X), corners.Min(p => p.Y), corners.Max(p => p.X), corners.Max(p => p.Y));
    }

    private static Bounds GetApproximateBounds(string? d)
    {
        if (string.IsNullOrWhiteSpace(d)) return Bounds.Invalid;
        var tokens = TokenRegex.Matches(d).Select(m => m.Value).ToArray();
        var values = tokens.Where(t => char.IsDigit(t[0]) || t[0] is '+' or '-' or '.')
            .Select(t => double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? (double?)n : null)
            .Where(n => n.HasValue).Select(n => n!.Value).ToArray();
        if (values.Length < 4) return Bounds.Invalid;
        // Path data emitted by the current vectorizer consists predominantly of M/L/C/Q coordinates.
        // Pairwise token bounds are conservative for these commands, but arc flags are not handled specially.
        var xs = new List<double>();
        var ys = new List<double>();
        for (var i = 0; i + 1 < values.Length; i += 2) { xs.Add(values[i]); ys.Add(values[i + 1]); }
        if (xs.Count == 0) return Bounds.Invalid;
        return new(xs.Min(), ys.Min(), xs.Max(), ys.Max());
    }

    private static Bounds ParseViewBox(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Bounds.Invalid;
        var n = NumberRegex.Matches(text).Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
        return n.Length == 4 ? new(n[0], n[1], n[0] + n[2], n[1] + n[3]) : Bounds.Invalid;
    }

    private static string F(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
    private sealed record PathCandidate(int Index, XElement Element, Bounds Bounds);
    private readonly record struct Bounds(double MinX, double MinY, double MaxX, double MaxY)
    {
        public static Bounds Invalid => new(double.NaN, double.NaN, double.NaN, double.NaN);
        public bool IsValid => double.IsFinite(MinX) && double.IsFinite(MinY) && double.IsFinite(MaxX) && double.IsFinite(MaxY) && MaxX > MinX && MaxY > MinY;
        public double Width => MaxX - MinX;
        public double Height => MaxY - MinY;
        public Bounds Union(Bounds other) => new(Math.Min(MinX, other.MinX), Math.Min(MinY, other.MinY), Math.Max(MaxX, other.MaxX), Math.Max(MaxY, other.MaxY));
    }
}
