using System.Globalization;
using System.Text;
using Docnet.Core;
using Docnet.Core.Models;
using HamiChap_MES_ERP.Models.Imposition;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics;

namespace HamiChap_MES_ERP.Services.Imposition;

/// <summary>
/// Converts a PDF artwork page to SVG geometry.
/// It prefers closed vector contours already present in the PDF. If no usable closed
/// contours are found, it rasterizes the page and traces foreground pixel boundaries.
/// Raster-traced output is always flagged for human review because printed artwork,
/// text, crease lines, and cut lines cannot reliably be distinguished from pixels alone.
/// </summary>
public sealed class ArtworkVectorizationService
{
    public async Task<ArtworkVectorizationResult> VectorizeAsync(
        Stream pdfStream,
        string outputDirectory,
        ArtworkVectorizationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("An output directory is required.", nameof(outputDirectory));

        options ??= new ArtworkVectorizationOptions();
        if (options.PageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(options.PageNumber), "PDF page numbers start at 1.");

        await using var buffer = new MemoryStream();
        await pdfStream.CopyToAsync(buffer, cancellationToken);
        var pdfBytes = buffer.ToArray();
        if (pdfBytes.Length == 0)
            throw new ArgumentException("The uploaded PDF is empty.", nameof(pdfStream));

        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, $"artwork-vector-{Guid.NewGuid():N}.svg");

        using var document = PdfDocument.Open(new MemoryStream(pdfBytes));
        if (options.PageNumber > document.NumberOfPages)
            throw new ArgumentOutOfRangeException(nameof(options.PageNumber), $"The PDF has {document.NumberOfPages} pages.");

        var page = document.GetPage(options.PageNumber);
        var vectorResult = TryExportExistingVectors(page, outputPath);
        if (vectorResult.ClosedContours > 0)
        {
            return new ArtworkVectorizationResult(
                true,
                ArtworkVectorSource.ExistingPdfVectorPaths,
                page.Number,
                vectorResult.TotalPaths,
                vectorResult.ClosedContours,
                vectorResult.ClosedContours,
                page.Width,
                page.Height,
                outputPath,
                true,
                "Existing PDF vector paths were extracted. Review the contour classification before nesting.",
                new[]
                {
                    "The SVG preserves detected closed vector subpaths, but this service does not yet know which paths are cut lines versus crease, artwork, or clipping geometry.",
                    "Confirm the correct cut contour before using this geometry for production nesting."
                });
        }

        cancellationToken.ThrowIfCancellationRequested();
        var raster = RenderPage(pdfBytes, options.PageNumber - 1, options.RasterMaxDimension);
        var contours = TraceRasterBoundaries(raster.Pixels, raster.Width, raster.Height, options);
        if (contours.Count == 0)
        {
            return new ArtworkVectorizationResult(
                false, null, page.Number, vectorResult.TotalPaths, 0, 0,
                page.Width, page.Height, null, true,
                "No usable closed PDF contours or raster contours were detected.",
                new[] { "Try another page, adjust the raster threshold, or upload a higher-resolution source." });
        }

        var svg = BuildRasterSvg(contours, raster.Width, raster.Height, page.Width, page.Height);
        await File.WriteAllTextAsync(outputPath, svg, new UTF8Encoding(false), cancellationToken);

        return new ArtworkVectorizationResult(
            true,
            ArtworkVectorSource.TracedFromRasterRender,
            page.Number,
            vectorResult.TotalPaths,
            0,
            contours.Count,
            page.Width,
            page.Height,
            outputPath,
            true,
            "No usable closed vector contours were found. A raster-based SVG trace was generated and requires review.",
            new[]
            {
                "Raster tracing follows visible pixel boundaries; it is not equivalent to recovering the original CAD/cut path.",
                "Printed graphics, text, cut lines, and crease lines may all be traced. Remove unwanted contours and confirm the intended cut path before production."
            });
    }

    private static (int TotalPaths, int ClosedContours) TryExportExistingVectors(
        UglyToad.PdfPig.Content.Page page, string outputPath)
    {
        var totalPaths = page.Paths.Count;
        var closedSubpaths = new List<PdfSubpath>();
        foreach (var path in page.Paths)
        {
            if (path.IsClipping) continue;
            foreach (var subpath in path)
            {
                if (subpath.Commands.Count >= 4 && subpath.IsClosed())
                    closedSubpaths.Add(subpath);
            }
        }

        if (closedSubpaths.Count == 0)
            return (totalPaths, 0);

        var svg = new StringBuilder();
        svg.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"")
            .Append(F(page.Width)).Append("pt\" height=\"").Append(F(page.Height))
            .Append("pt\" viewBox=\"0 0 ").Append(F(page.Width)).Append(' ').Append(F(page.Height)).AppendLine("\">");
        svg.AppendLine("<g fill=\"none\" stroke=\"#111111\" stroke-width=\"0.25\" vector-effect=\"non-scaling-stroke\">");
        foreach (var subpath in closedSubpaths)
        {
            var d = new StringBuilder();
            foreach (var command in subpath.Commands)
                command.WriteSvg(d, page.Height);
            svg.Append("<path d=\"").Append(System.Security.SecurityElement.Escape(d.ToString().Trim()))
                .AppendLine("\" data-source=\"pdf-vector\" />");
        }
        svg.AppendLine("</g>");
        svg.AppendLine("</svg>");
        File.WriteAllText(outputPath, svg.ToString(), new UTF8Encoding(false));
        return (totalPaths, closedSubpaths.Count);
    }

    private static (byte[] Pixels, int Width, int Height) RenderPage(byte[] pdfBytes, int zeroBasedPage, int maxDimension)
    {
        maxDimension = Math.Clamp(maxDimension, 256, 2400);
        using var docReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions(maxDimension, maxDimension));
        using var pageReader = docReader.GetPageReader(zeroBasedPage);
        var width = pageReader.GetPageWidth();
        var height = pageReader.GetPageHeight();
        var pixels = pageReader.GetImage();
        if (width <= 0 || height <= 0 || pixels.Length < width * height * 4)
            throw new InvalidOperationException("The PDF page could not be rasterized for contour tracing.");
        return (pixels, width, height);
    }

    private static List<List<(int X, int Y)>> TraceRasterBoundaries(
        byte[] bgra, int width, int height, ArtworkVectorizationOptions options)
    {
        var edges = new HashSet<Edge>();
        var foreground = new bool[checked(width * height)];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * width + x) * 4;
                // Docnet returns BGRA. Luminance is adequate for the first-pass threshold trace.
                var luminance = (byte)((bgra[i + 2] * 299 + bgra[i + 1] * 587 + bgra[i] * 114) / 1000);
                foreground[y * width + x] = bgra[i + 3] > 0 && luminance < options.RasterThreshold;
            }
        }

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!foreground[y * width + x]) continue;
                if (edges.Count > options.MaximumRasterEdges)
                    throw new InvalidOperationException("Raster trace is too noisy. Increase the threshold or use a cleaner/higher-quality source PDF.");

                AddOrCancel(edges, new Edge(x, y, x + 1, y));
                AddOrCancel(edges, new Edge(x + 1, y, x + 1, y + 1));
                AddOrCancel(edges, new Edge(x + 1, y + 1, x, y + 1));
                AddOrCancel(edges, new Edge(x, y + 1, x, y));
            }
        }

        var outgoing = new Dictionary<(int X, int Y), List<Edge>>();
        foreach (var edge in edges)
        {
            var start = (edge.X1, edge.Y1);
            if (!outgoing.TryGetValue(start, out var list)) outgoing[start] = list = new List<Edge>();
            list.Add(edge);
        }

        var unused = new HashSet<Edge>(edges);
        var contours = new List<List<(int X, int Y)>>();
        while (unused.Count > 0)
        {
            var first = unused.First();
            var contour = new List<(int X, int Y)> { (first.X1, first.Y1) };
            var current = first;
            unused.Remove(current);
            contour.Add((current.X2, current.Y2));
            var guard = 0;
            while ((current.X2, current.Y2) != contour[0] && guard++ <= edges.Count)
            {
                if (!outgoing.TryGetValue((current.X2, current.Y2), out var nextEdges)) break;
                var next = nextEdges.FirstOrDefault(unused.Contains);
                if (next == default) break;
                unused.Remove(next);
                current = next;
                contour.Add((current.X2, current.Y2));
            }

            if (contour.Count < 5 || contour[^1] != contour[0]) continue;
            contour = SimplifyOrthogonalContour(contour);
            var area = Math.Abs(SignedArea(contour));
            if (area >= options.MinimumRasterContourAreaPixels)
                contours.Add(contour);
        }
        return contours;
    }

    private static string BuildRasterSvg(
        IReadOnlyList<List<(int X, int Y)>> contours,
        int pixelWidth, int pixelHeight, double pageWidthPoints, double pageHeightPoints)
    {
        var svg = new StringBuilder();
        svg.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").Append(F(pageWidthPoints))
            .Append("pt\" height=\"").Append(F(pageHeightPoints)).Append("pt\" viewBox=\"0 0 ")
            .Append(pixelWidth).Append(' ').Append(pixelHeight).AppendLine("\">");
        svg.AppendLine("<g fill=\"none\" stroke=\"#111111\" stroke-width=\"1\" stroke-linejoin=\"round\">");
        foreach (var contour in contours)
        {
            svg.Append("<path d=\"M ").Append(contour[0].X).Append(' ').Append(contour[0].Y);
            for (var i = 1; i < contour.Count; i++)
                svg.Append(" L ").Append(contour[i].X).Append(' ').Append(contour[i].Y);
            svg.AppendLine(" Z\" data-source=\"raster-trace\" />");
        }
        svg.AppendLine("</g></svg>");
        return svg.ToString();
    }

    private static void AddOrCancel(HashSet<Edge> edges, Edge edge)
    {
        var reverse = new Edge(edge.X2, edge.Y2, edge.X1, edge.Y1);
        if (!edges.Remove(reverse)) edges.Add(edge);
    }

    private static List<(int X, int Y)> SimplifyOrthogonalContour(List<(int X, int Y)> points)
    {
        if (points.Count < 4) return points;
        var result = new List<(int X, int Y)>();
        for (var i = 0; i < points.Count - 1; i++)
        {
            var previous = points[(i - 1 + points.Count - 1) % (points.Count - 1)];
            var current = points[i];
            var next = points[i + 1];
            if ((previous.X == current.X && current.X == next.X) ||
                (previous.Y == current.Y && current.Y == next.Y)) continue;
            result.Add(current);
        }
        if (result.Count > 0) result.Add(result[0]);
        return result.Count >= 4 ? result : points;
    }

    private static double SignedArea(IReadOnlyList<(int X, int Y)> points)
    {
        double area = 0;
        for (var i = 0; i < points.Count - 1; i++)
            area += points[i].X * (double)points[i + 1].Y - points[i + 1].X * (double)points[i].Y;
        return area / 2.0;
    }

    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private readonly record struct Edge(int X1, int Y1, int X2, int Y2);
}
