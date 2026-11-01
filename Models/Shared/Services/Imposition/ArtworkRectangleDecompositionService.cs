using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HamiChap_MES_ERP.Models.Imposition;

namespace HamiChap_MES_ERP.Services.Imposition;

/// <summary>
/// Builds a conservative, non-overlapping rectangular cover from the geometry in the
/// supplied primary-contour SVG. It never opens the source PDF or the all-contours SVG.
/// Rectangles are a coarse collision representation; retain the source vector for final checks.
/// </summary>
public sealed class ArtworkRectangleDecompositionService
{
    private static readonly Regex TokenRegex = new(
        @"[A-Za-z]|[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?",
        RegexOptions.Compiled);
    private static readonly Regex TransformRegex = new(
        @"([A-Za-z]+)\s*\(([^)]*)\)", RegexOptions.Compiled);
    private static readonly Regex NumberRegex = new(
        @"[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?", RegexOptions.Compiled);

    public async Task<ArtworkRectangleDecompositionResult> DecomposeAsync(
        string normalizedSvgPath,
        string outputDirectory,
        decimal gridResolutionMm = 1m,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(normalizedSvgPath) || !File.Exists(normalizedSvgPath))
            throw new FileNotFoundException("The primary-contour SVG could not be found.", normalizedSvgPath);
        if (gridResolutionMm <= 0 || gridResolutionMm > 10)
            throw new ArgumentOutOfRangeException(nameof(gridResolutionMm), "Grid resolution must be greater than 0 and at most 10 mm.");

        // The caller supplies _geometry.PrimaryContourSvgPath_. This service deliberately
        // consumes only that SVG and does not fall back to the source PDF or all-contours SVG.
        var document = XDocument.Load(normalizedSvgPath, LoadOptions.None);
        var root = document.Root ?? throw new InvalidDataException("SVG root is missing.");
        if (!root.Name.LocalName.Equals("svg", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The supplied file is not an SVG document.");

        var widthMm = ReadMm((string?)root.Attribute("width"));
        var heightMm = ReadMm((string?)root.Attribute("height"));
        if (widthMm <= 0 || heightMm <= 0)
            throw new InvalidDataException("The primary-contour SVG must have physical width and height in millimetres.");

        var pathElements = root.Descendants().Where(e => e.Name.LocalName == "path" &&
            !string.IsNullOrWhiteSpace((string?)e.Attribute("d"))).ToList();
        if (pathElements.Count == 0)
            return Failure(widthMm, heightMm, gridResolutionMm, "The primary-contour SVG contains no path geometry.");

        var rings = new List<List<Point>>();
        foreach (var path in pathElements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pathRings = FlattenPathIntoRings((string)path.Attribute("d")!);
            if (pathRings.Count == 0)
                continue;

            var transforms = path.AncestorsAndSelf()
                .Where(e => e.Attribute("transform") is not null)
                .ToList();
            foreach (var ring in pathRings)
            {
                var transformed = ring.Select(p => ApplyTransforms(p, transforms)).ToList();
                if (transformed.Count >= 3)
                    rings.Add(transformed);
            }
        }

        if (rings.Count == 0)
            return Failure(widthMm, heightMm, gridResolutionMm,
                "No closed, usable contour subpaths were found in the primary-contour SVG.");

        // Convert SVG viewBox units into the physical millimetre coordinate system. The
        // standardized primary SVG normally has viewBox="0 0 width height"; handling a
        // non-zero viewBox origin also prevents shifted contours from being sampled incorrectly.
        var viewBox = ParseViewBox((string?)root.Attribute("viewBox"), widthMm, heightMm);
        var sx = (double)widthMm / viewBox.Width;
        var sy = (double)heightMm / viewBox.Height;
        rings = rings.Select(r => r.Select(p => new Point((p.X - viewBox.MinX) * sx,
                                                           (p.Y - viewBox.MinY) * sy)).ToList()).ToList();

        var allPoints = rings.SelectMany(r => r).ToList();
        var minX = Math.Max(0d, allPoints.Min(p => p.X));
        var minY = Math.Max(0d, allPoints.Min(p => p.Y));
        var maxX = Math.Min((double)widthMm, allPoints.Max(p => p.X));
        var maxY = Math.Min((double)heightMm, allPoints.Max(p => p.Y));
        if (maxX <= minX || maxY <= minY)
            return Failure(widthMm, heightMm, gridResolutionMm,
                "The primary contour is outside the physical SVG bounds. Check its transforms and viewBox.");

        var cols = checked((int)Math.Ceiling((double)widthMm / (double)gridResolutionMm));
        var rows = checked((int)Math.Ceiling((double)heightMm / (double)gridResolutionMm));
        if ((long)cols * rows > 4_000_000)
            throw new InvalidOperationException("The requested grid is too detailed. Increase the grid resolution (for example, to 2 mm).");

        var occupied = new bool[rows, cols];
        for (var y = 0; y < rows; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var y0 = (double)(y * gridResolutionMm);
            var y1 = Math.Min((double)heightMm, (double)((y + 1) * gridResolutionMm));
            if (y1 <= minY || y0 >= maxY) continue;
            for (var x = 0; x < cols; x++)
            {
                var x0 = (double)(x * gridResolutionMm);
                var x1 = Math.Min((double)widthMm, (double)((x + 1) * gridResolutionMm));
                if (x1 <= minX || x0 >= maxX) continue;
                occupied[y, x] = CellTouchesFilledGeometry(rings, x0, y0, x1, y1);
            }
        }

        // Merge each occupied grid cell exactly once. Therefore generated rectangles cannot
        // overlap one another. Boundary cells are conservatively included; holes remain empty
        // except for the grid cells touching their boundaries.
        var used = new bool[rows, cols];
        var rectangles = new List<ArtworkOccupiedRectangle>();
        for (var y = 0; y < rows; y++)
        for (var x = 0; x < cols; x++)
        {
            if (!occupied[y, x] || used[y, x]) continue;
            var xEnd = x;
            while (xEnd + 1 < cols && occupied[y, xEnd + 1] && !used[y, xEnd + 1]) xEnd++;
            var yEnd = y;
            while (yEnd + 1 < rows)
            {
                var canExtend = true;
                for (var xx = x; xx <= xEnd; xx++)
                {
                    if (occupied[yEnd + 1, xx] && !used[yEnd + 1, xx]) continue;
                    canExtend = false;
                    break;
                }
                if (!canExtend) break;
                yEnd++;
            }

            for (var yy = y; yy <= yEnd; yy++)
            for (var xx = x; xx <= xEnd; xx++) used[yy, xx] = true;

            var rx = x * gridResolutionMm;
            var ry = y * gridResolutionMm;
            var rw = Math.Min(widthMm, (xEnd + 1) * gridResolutionMm) - rx;
            var rh = Math.Min(heightMm, (yEnd + 1) * gridResolutionMm) - ry;
            if (rw > 0 && rh > 0)
                rectangles.Add(new ArtworkOccupiedRectangle(rx, ry, rw, rh));
        }

        if (rectangles.Count == 0)
            return Failure(widthMm, heightMm, gridResolutionMm, "The primary contour produced no occupied grid cells.");

        Directory.CreateDirectory(outputDirectory);
        var previewPath = Path.Combine(outputDirectory, $"artwork-rectangles-{Guid.NewGuid():N}.svg");
        var preview = CreatePreview(widthMm, heightMm, rings, rectangles);
        await File.WriteAllTextAsync(previewPath, preview, new UTF8Encoding(false), cancellationToken);

        return new ArtworkRectangleDecompositionResult(
            true, widthMm, heightMm, gridResolutionMm, rectangles, previewPath, true,
            $"Generated {rectangles.Count} non-overlapping rectangles from the primary-contour SVG at {gridResolutionMm:0.###} mm resolution.",
            new[]
            {
                $"Input: {Path.GetFileName(normalizedSvgPath)} (primary-contour SVG only).",
                "All closed path subpaths in the primary SVG are considered together using an even-odd fill rule; nested rings therefore remain holes/cutouts.",
                "The rectangle cover is conservative: cells touching a contour edge are included, so the cover can extend outside the exact contour by approximately one grid cell.",
                "Supported path commands: M, L, H, V, C, Q and Z. Unsupported SVG commands fail explicitly instead of silently generating incorrect fitting geometry.",
                "Use these rectangles for coarse collision checks only. Preserve the original primary SVG for final contour-level collision validation."
            });
    }

    private static ArtworkRectangleDecompositionResult Failure(decimal w, decimal h, decimal resolution, string message) =>
        new(false, w, h, resolution, Array.Empty<ArtworkOccupiedRectangle>(), null, true, message,
            new[] { "No rectangles were returned because the primary contour could not be interpreted safely." });

    private static string CreatePreview(decimal w, decimal h, IReadOnlyList<List<Point>> rings,
        IReadOnlyList<ArtworkOccupiedRectangle> rectangles)
    {
        var sb = new StringBuilder();
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").Append(F(w)).Append("mm\" height=\"").Append(F(h))
          .Append("mm\" viewBox=\"0 0 ").Append(F(w)).Append(' ').Append(F(h)).AppendLine("\">");
        sb.AppendLine("<rect x=\"0\" y=\"0\" width=\"100%\" height=\"100%\" fill=\"white\" stroke=\"#222\" stroke-width=\"0.2\"/>");
        foreach (var r in rectangles)
            sb.Append("<rect x=\"").Append(F(r.Xmm)).Append("\" y=\"").Append(F(r.Ymm)).Append("\" width=\"").Append(F(r.WidthMm))
              .Append("\" height=\"").Append(F(r.HeightMm)).AppendLine("\" fill=\"#c8e6ff\" stroke=\"#145da0\" stroke-width=\"0.12\"/>");
        foreach (var ring in rings)
        {
            sb.Append("<polygon points=\"");
            foreach (var p in ring) sb.Append(F(p.X)).Append(',').Append(F(p.Y)).Append(' ');
            sb.AppendLine("\" fill=\"none\" stroke=\"#d22\" stroke-width=\"0.25\"/>");
        }
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static List<List<Point>> FlattenPathIntoRings(string d)
    {
        var tokens = TokenRegex.Matches(d).Select(m => m.Value).ToList();
        var rings = new List<List<Point>>();
        List<Point>? current = null;
        var i = 0;
        char command = '\0';
        double x = 0, y = 0, startX = 0, startY = 0;
        bool HasNumber() => i < tokens.Count && !char.IsLetter(tokens[i][0]);
        double Number()
        {
            if (!HasNumber()) throw new InvalidDataException("SVG path command has too few numeric parameters.");
            return double.Parse(tokens[i++], NumberStyles.Float, CultureInfo.InvariantCulture);
        }
        void Add(Point p)
        {
            current ??= new List<Point>();
            if (current.Count == 0 || DistanceSquared(current[^1], p) > 1e-16) current.Add(p);
        }
        void CloseCurrent()
        {
            if (current is { Count: >= 3 })
            {
                if (DistanceSquared(current[0], current[^1]) > 1e-16) current.Add(current[0]);
                rings.Add(current);
            }
            current = null;
        }

        while (i < tokens.Count)
        {
            if (char.IsLetter(tokens[i][0])) command = tokens[i++][0];
            if (command == '\0') throw new InvalidDataException("SVG path data contains numbers without a command.");
            var relative = char.IsLower(command);
            var c = char.ToUpperInvariant(command);

            if (c == 'Z')
            {
                CloseCurrent();
                x = startX; y = startY; command = '\0';
                continue;
            }
            if (!HasNumber()) throw new InvalidDataException($"SVG path command '{command}' is missing parameters.");

            switch (c)
            {
                case 'M':
                case 'L':
                {
                    var nx = Number(); var ny = Number();
                    if (relative) { nx += x; ny += y; }
                    x = nx; y = ny;
                    if (c == 'M')
                    {
                        if (current is not null)
                            throw new InvalidDataException("An SVG subpath must be explicitly closed with Z before another M command.");
                        startX = x; startY = y; Add(new Point(x, y));
                        command = relative ? 'l' : 'L';
                    }
                    else Add(new Point(x, y));
                    break;
                }
                case 'H':
                {
                    var nx = Number(); x = relative ? x + nx : nx; Add(new Point(x, y)); break;
                }
                case 'V':
                {
                    var ny = Number(); y = relative ? y + ny : ny; Add(new Point(x, y)); break;
                }
                case 'C':
                {
                    var x0 = x; var y0 = y;
                    var x1 = Number(); var y1 = Number(); var x2 = Number(); var y2 = Number(); var ex = Number(); var ey = Number();
                    if (relative) { x1 += x0; y1 += y0; x2 += x0; y2 += y0; ex += x0; ey += y0; }
                    for (var step = 1; step <= 24; step++)
                    {
                        var t = step / 24d; var mt = 1 - t;
                        Add(new Point(mt * mt * mt * x0 + 3 * mt * mt * t * x1 + 3 * mt * t * t * x2 + t * t * t * ex,
                                      mt * mt * mt * y0 + 3 * mt * mt * t * y1 + 3 * mt * t * t * y2 + t * t * t * ey));
                    }
                    x = ex; y = ey; break;
                }
                case 'Q':
                {
                    var x0 = x; var y0 = y;
                    var x1 = Number(); var y1 = Number(); var ex = Number(); var ey = Number();
                    if (relative) { x1 += x0; y1 += y0; ex += x0; ey += y0; }
                    for (var step = 1; step <= 20; step++)
                    {
                        var t = step / 20d; var mt = 1 - t;
                        Add(new Point(mt * mt * x0 + 2 * mt * t * x1 + t * t * ex,
                                      mt * mt * y0 + 2 * mt * t * y1 + t * t * ey));
                    }
                    x = ex; y = ey; break;
                }
                default:
                    throw new NotSupportedException($"SVG path command '{command}' is not supported. No approximate rectangles were generated.");
            }
        }
        if (current is not null)
            throw new InvalidDataException("An SVG contour subpath is not explicitly closed with Z.");
        return rings;
    }

    private static Point ApplyTransforms(Point point, IReadOnlyList<XElement> elements)
    {
        // SVG element transforms are applied from the path outward through its parent groups.
        foreach (var element in elements)
        {
            var text = (string?)element.Attribute("transform");
            if (string.IsNullOrWhiteSpace(text)) continue;
            foreach (Match match in TransformRegex.Matches(text))
            {
                var name = match.Groups[1].Value;
                var v = NumberRegex.Matches(match.Groups[2].Value)
                    .Select(m => double.Parse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
                var p = point;
                switch (name.ToLowerInvariant())
                {
                    case "translate" when v.Length >= 1:
                        point = new Point(p.X + v[0], p.Y + (v.Length > 1 ? v[1] : 0)); break;
                    case "scale" when v.Length >= 1:
                        point = new Point(p.X * v[0], p.Y * (v.Length > 1 ? v[1] : v[0])); break;
                    case "rotate" when v.Length >= 1:
                    {
                        var cx = v.Length > 2 ? v[1] : 0; var cy = v.Length > 2 ? v[2] : 0;
                        var a = v[0] * Math.PI / 180d; var dx = p.X - cx; var dy = p.Y - cy;
                        point = new Point(cx + dx * Math.Cos(a) - dy * Math.Sin(a), cy + dx * Math.Sin(a) + dy * Math.Cos(a));
                        break;
                    }
                    case "matrix" when v.Length == 6:
                        point = new Point(v[0] * p.X + v[2] * p.Y + v[4], v[1] * p.X + v[3] * p.Y + v[5]); break;
                    case "skewx" when v.Length == 1:
                        point = new Point(p.X + Math.Tan(v[0] * Math.PI / 180d) * p.Y, p.Y); break;
                    case "skewy" when v.Length == 1:
                        point = new Point(p.X, p.Y + Math.Tan(v[0] * Math.PI / 180d) * p.X); break;
                    default:
                        throw new NotSupportedException($"SVG transform '{name}' is unsupported. No rectangles were generated.");
                }
            }
        }
        return point;
    }

    private static bool CellTouchesFilledGeometry(IReadOnlyList<List<Point>> rings,
        double x0, double y0, double x1, double y1)
    {
        var probes = new[] { new Point(x0, y0), new Point(x1, y0), new Point(x1, y1),
            new Point(x0, y1), new Point((x0 + x1) / 2, (y0 + y1) / 2) };
        if (probes.Any(p => IsInsideEvenOdd(p, rings))) return true;

        // A contour crossing the cell means this is a boundary cell. Include it conservatively,
        // including boundaries around holes, so no rectangle crosses the actual cut edge.
        foreach (var ring in rings)
        for (var i = 0; i < ring.Count - 1; i++)
        {
            var a = ring[i]; var b = ring[i + 1];
            if (SegmentIntersects(a, b, new Point(x0, y0), new Point(x1, y0)) ||
                SegmentIntersects(a, b, new Point(x1, y0), new Point(x1, y1)) ||
                SegmentIntersects(a, b, new Point(x1, y1), new Point(x0, y1)) ||
                SegmentIntersects(a, b, new Point(x0, y1), new Point(x0, y0))) return true;
        }
        return false;
    }

    private static bool IsInsideEvenOdd(Point point, IReadOnlyList<List<Point>> rings)
    {
        var inside = false;
        foreach (var ring in rings)
        {
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                var a = ring[i]; var b = ring[j];
                if ((a.Y > point.Y) != (b.Y > point.Y) &&
                    point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                    inside = !inside;
            }
        }
        return inside;
    }

    private static bool SegmentIntersects(Point a, Point b, Point c, Point d)
    {
        static double Cross(Point p, Point q, Point r) => (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);
        var c1 = Cross(a, b, c); var c2 = Cross(a, b, d); var c3 = Cross(c, d, a); var c4 = Cross(c, d, b);
        const double eps = 1e-10;
        return c1 * c2 <= eps && c3 * c4 <= eps;
    }

    private static ViewBox ParseViewBox(string? value, decimal widthMm, decimal heightMm)
    {
        if (string.IsNullOrWhiteSpace(value)) return new ViewBox(0, 0, (double)widthMm, (double)heightMm);
        var values = NumberRegex.Matches(value).Select(m => double.Parse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
        if (values.Length != 4 || values[2] <= 0 || values[3] <= 0)
            throw new InvalidDataException("The primary-contour SVG has an invalid viewBox.");
        return new ViewBox(values[0], values[1], values[2], values[3]);
    }

    private static decimal ReadMm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        var match = Regex.Match(value, @"^\s*([-+]?\d*\.?\d+)\s*mm\s*$", RegexOptions.IgnoreCase);
        return match.Success && decimal.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    private static double DistanceSquared(Point a, Point b)
    {
        var dx = a.X - b.X; var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    private static string F(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private readonly record struct Point(double X, double Y);
    private readonly record struct ViewBox(double MinX, double MinY, double Width, double Height);
}
