using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HamiChap_MES_ERP.Models.Imposition;

namespace HamiChap_MES_ERP.Services.Imposition;

/// <summary>
/// Converts a normalized, single-contour SVG into a conservative rectangular cover.
/// It samples a configurable grid and merges occupied cells into non-overlapping rectangles.
/// This is a fitting/collision approximation, not a replacement for the source vector.
/// </summary>
public sealed class ArtworkRectangleDecompositionService
{
    private static readonly XNamespace SvgNs = "http://www.w3.org/2000/svg";
    private static readonly Regex TokenRegex = new(@"[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?|[A-Za-z]", RegexOptions.Compiled);

    public async Task<ArtworkRectangleDecompositionResult> DecomposeAsync(
        string normalizedSvgPath,
        string outputDirectory,
        decimal gridResolutionMm = 1m,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(normalizedSvgPath) || !File.Exists(normalizedSvgPath))
            throw new FileNotFoundException("The normalized primary SVG could not be found.", normalizedSvgPath);
        if (gridResolutionMm <= 0 || gridResolutionMm > 10)
            throw new ArgumentOutOfRangeException(nameof(gridResolutionMm), "Grid resolution must be greater than 0 and at most 10 mm.");

        var doc = XDocument.Load(normalizedSvgPath);
        var root = doc.Root ?? throw new InvalidDataException("SVG root is missing.");
        var widthMm = ReadMm((string?)root.Attribute("width"));
        var heightMm = ReadMm((string?)root.Attribute("height"));
        if (widthMm <= 0 || heightMm <= 0)
            throw new InvalidDataException("The normalized SVG must have physical width and height in millimetres.");

        var path = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "path");
        if (path is null || string.IsNullOrWhiteSpace((string?)path.Attribute("d")))
            return Failure(widthMm, heightMm, gridResolutionMm, "No usable SVG path was found.");

        var pathData = (string)path.Attribute("d")!;
        if (pathData.IndexOfAny(new[] { 'Z', 'z' }) < 0)
            return Failure(widthMm, heightMm, gridResolutionMm, "The selected SVG path is not explicitly closed; a safe interior decomposition cannot be generated.");
        var polygon = ApplyAncestorTransforms(FlattenPath(pathData), path);
        if (polygon.Count < 3)
            return Failure(widthMm, heightMm, gridResolutionMm,
                "The contour could not be converted to a closed polygon. This path may use unsupported SVG commands.");

        // Grid cells are occupied conservatively if their center or any corner is inside the
        // polygon, or if any polygon edge intersects the cell. This intentionally slightly
        // overestimates occupied area rather than allowing a nesting engine to place artwork
        // inside material that might actually be occupied.
        var cols = checked((int)Math.Ceiling((double)(widthMm / gridResolutionMm)));
        var rows = checked((int)Math.Ceiling((double)(heightMm / gridResolutionMm)));
        if ((long)cols * rows > 4_000_000)
            throw new InvalidOperationException("The requested grid is too detailed for this artwork. Increase grid resolution (for example, to 2 mm).");

        var occupied = new bool[rows, cols];
        for (var y = 0; y < rows; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var y0 = (double)(y * gridResolutionMm);
            var y1 = Math.Min((double)heightMm, (double)((y + 1) * gridResolutionMm));
            for (var x = 0; x < cols; x++)
            {
                var x0 = (double)(x * gridResolutionMm);
                var x1 = Math.Min((double)widthMm, (double)((x + 1) * gridResolutionMm));
                occupied[y, x] = CellTouchesPolygon(polygon, x0, y0, x1, y1);
            }
        }

        // Greedy maximal horizontal runs, extended vertically while all cells remain occupied.
        // Every occupied cell is emitted exactly once, so rectangles never overlap each other.
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
                    if (!occupied[yEnd + 1, xx] || used[yEnd + 1, xx]) { canExtend = false; break; }
                if (!canExtend) break;
                yEnd++;
            }
            for (var yy = y; yy <= yEnd; yy++)
            for (var xx = x; xx <= xEnd; xx++) used[yy, xx] = true;

            var rx = x * gridResolutionMm;
            var ry = y * gridResolutionMm;
            var rw = Math.Min(widthMm, (xEnd + 1) * gridResolutionMm) - rx;
            var rh = Math.Min(heightMm, (yEnd + 1) * gridResolutionMm) - ry;
            rectangles.Add(new(rx, ry, rw, rh));
        }

        Directory.CreateDirectory(outputDirectory);
        var previewPath = Path.Combine(outputDirectory, $"artwork-rectangles-{Guid.NewGuid():N}.svg");
        await File.WriteAllTextAsync(previewPath, CreatePreview(widthMm, heightMm, rectangles), new UTF8Encoding(false), cancellationToken);
        return new(true, widthMm, heightMm, gridResolutionMm, rectangles, previewPath, true,
            $"Generated {rectangles.Count} non-overlapping rectangles at {gridResolutionMm:0.###} mm grid resolution.",
            new[]
            {
                "The rectangular cover is conservative and can extend beyond the exact contour by up to approximately one grid cell around edges.",
                "This implementation handles SVG M/L/H/V/C/Q/Z path commands. Unsupported commands such as elliptical arc (A) cause contour conversion to fail rather than silently produce a false fit.",
                "Use the rectangles for coarse collision checks only; preserve and use the original contour for final geometry validation.",
                "The service processes the first path only. Confirm that the selected primary SVG contains the actual closed cut contour, not a crease line or printed graphic."
            });
    }

    private static ArtworkRectangleDecompositionResult Failure(decimal w, decimal h, decimal resolution, string message) =>
        new(false, w, h, resolution, Array.Empty<ArtworkOccupiedRectangle>(), null, true, message,
            new[] { "No rectangles were returned because the contour could not be safely interpreted." });

    private static string CreatePreview(decimal w, decimal h, IReadOnlyList<ArtworkOccupiedRectangle> rectangles)
    {
        var sb = new StringBuilder();
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").Append(F(w)).Append("mm\" height=\"").Append(F(h))
          .Append("mm\" viewBox=\"0 0 ").Append(F(w)).Append(' ').Append(F(h)).AppendLine("\">");
        sb.AppendLine("<rect x=\"0\" y=\"0\" width=\"100%\" height=\"100%\" fill=\"white\" stroke=\"#222\" stroke-width=\"0.2\"/>");
        foreach (var r in rectangles)
            sb.Append("<rect x=\"").Append(F(r.Xmm)).Append("\" y=\"").Append(F(r.Ymm)).Append("\" width=\"").Append(F(r.WidthMm)).Append("\" height=\"").Append(F(r.HeightMm)).AppendLine("\" fill=\"#c8e6ff\" stroke=\"#145da0\" stroke-width=\"0.15\"/>");
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static List<(double X, double Y)> FlattenPath(string d)
    {
        var tokens = TokenRegex.Matches(d).Select(m => m.Value).ToList();
        var points = new List<(double X, double Y)>();
        var i = 0; char command = '\0';
        double x = 0, y = 0, sx = 0, sy = 0;
        (double X, double Y) last = (0, 0);
        bool HasNumber() => i < tokens.Count && !char.IsLetter(tokens[i][0]);
        double Number() => double.Parse(tokens[i++], CultureInfo.InvariantCulture);
        while (i < tokens.Count)
        {
            if (char.IsLetter(tokens[i][0])) command = tokens[i++][0];
            if (command == '\0') throw new InvalidDataException("SVG path starts without a command.");
            var relative = char.IsLower(command); var c = char.ToUpperInvariant(command);
            if (c == 'Z') { Add((sx, sy)); x = sx; y = sy; command = '\0'; continue; }
            if (c == 'M' || c == 'L')
            {
                if (!HasNumber()) continue;
                var nx = Number(); var ny = Number(); if (relative) { nx += x; ny += y; }
                x = nx; y = ny; Add((x, y));
                if (c == 'M') { sx = x; sy = y; command = relative ? 'l' : 'L'; }
            }
            else if (c == 'H') { if (!HasNumber()) continue; var nx = Number(); x = relative ? x + nx : nx; Add((x, y)); }
            else if (c == 'V') { if (!HasNumber()) continue; var ny = Number(); y = relative ? y + ny : ny; Add((x, y)); }
            else if (c == 'C')
            {
                if (!HasNumber()) continue;
                var x0=x; var y0=y;
                var a=Number(); var b=Number(); var cc=Number(); var dd=Number(); var e=Number(); var f=Number();
                if (relative) { a+=x0; b+=y0; cc+=x0; dd+=y0; e+=x0; f+=y0; }
                for (var step=1; step<=16; step++) { var t=step/16d; var mt=1-t; Add((mt*mt*mt*x0+3*mt*mt*t*a+3*mt*t*t*cc+t*t*t*e, mt*mt*mt*y0+3*mt*mt*t*b+3*mt*t*t*dd+t*t*t*f)); }
                x=e; y=f;
            }
            else if (c == 'Q')
            {
                if (!HasNumber()) continue;
                var x0=x; var y0=y; var a=Number(); var b=Number(); var e=Number(); var f=Number();
                if (relative) { a+=x0; b+=y0; e+=x0; f+=y0; }
                for (var step=1; step<=12; step++) { var t=step/12d; var mt=1-t; Add((mt*mt*x0+2*mt*t*a+t*t*e, mt*mt*y0+2*mt*t*b+t*t*f)); }
                x=e; y=f;
            }
            else throw new NotSupportedException($"SVG path command '{command}' is not supported for safe decomposition.");
        }
        return points;
        void Add((double X, double Y) p) { if (points.Count == 0 || Math.Abs(points[^1].X-p.X)>1e-8 || Math.Abs(points[^1].Y-p.Y)>1e-8) points.Add(p); last=p; }
    }

    private static List<(double X, double Y)> ApplyAncestorTransforms(List<(double X, double Y)> points, XElement path)
    {
        // SVG transforms on the generated geometry are nested: path rotation, translation,
        // then scale. Apply ancestor transforms from the innermost group outwards.
        var ancestors = path.Ancestors().Where(e => e.Attribute("transform") is not null).ToList();
        foreach (var ancestor in ancestors)
        {
            // Ancestors() is innermost-first, which is the correct application order for nested SVG groups.
            var transformText = (string)ancestor.Attribute("transform")!;
            foreach (Match match in Regex.Matches(transformText, @"([A-Za-z]+)\s*\(([^)]*)\)"))
            {
                var name = match.Groups[1].Value;
                var v = Regex.Matches(match.Groups[2].Value, @"[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?")
                    .Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
                for (var i = 0; i < points.Count; i++)
                {
                    var (x, y) = points[i];
                    if (name.Equals("scale", StringComparison.OrdinalIgnoreCase) && v.Length >= 1)
                    { var sy = v.Length > 1 ? v[1] : v[0]; points[i] = (x * v[0], y * sy); }
                    else if (name.Equals("translate", StringComparison.OrdinalIgnoreCase) && v.Length >= 1)
                    { points[i] = (x + v[0], y + (v.Length > 1 ? v[1] : 0)); }
                    else if (name.Equals("rotate", StringComparison.OrdinalIgnoreCase) && v.Length >= 1)
                    {
                        var cx = v.Length > 2 ? v[1] : 0; var cy = v.Length > 2 ? v[2] : 0;
                        var radians = v[0] * Math.PI / 180d; var dx = x - cx; var dy = y - cy;
                        points[i] = (cx + dx * Math.Cos(radians) - dy * Math.Sin(radians), cy + dx * Math.Sin(radians) + dy * Math.Cos(radians));
                    }
                    else if (!name.Equals("matrix", StringComparison.OrdinalIgnoreCase))
                        throw new NotSupportedException($"SVG transform '{name}' is not supported for safe decomposition.");
                    else if (v.Length == 6)
                        points[i] = (v[0] * x + v[2] * y + v[4], v[1] * x + v[3] * y + v[5]);
                }
            }
        }
        return points;
    }

    private static bool CellTouchesPolygon(IReadOnlyList<(double X, double Y)> p, double x0, double y0, double x1, double y1)
    {
        var corners = new[] { (x0,y0), (x1,y0), (x1,y1), (x0,y1), ((x0+x1)/2,(y0+y1)/2) };
        if (corners.Any(c => PointInPolygon(c.Item1, c.Item2, p))) return true;
        if (p.Any(v => v.X >= x0 && v.X <= x1 && v.Y >= y0 && v.Y <= y1)) return true;
        for (var i = 0; i < p.Count; i++)
        {
            var a = p[i]; var b = p[(i+1)%p.Count];
            if (SegmentIntersects(a, b, (x0,y0), (x1,y0)) || SegmentIntersects(a, b, (x1,y0), (x1,y1)) ||
                SegmentIntersects(a, b, (x1,y1), (x0,y1)) || SegmentIntersects(a, b, (x0,y1), (x0,y0))) return true;
        }
        return false;
    }
    private static bool PointInPolygon(double x, double y, IReadOnlyList<(double X,double Y)> p)
    {
        var inside=false;
        for (int i=0,j=p.Count-1;i<p.Count;j=i++)
        { var a=p[i]; var b=p[j]; if (((a.Y>y)!=(b.Y>y)) && x < (b.X-a.X)*(y-a.Y)/(b.Y-a.Y)+a.X) inside=!inside; }
        return inside;
    }
    private static bool SegmentIntersects((double X,double Y) a,(double X,double Y) b,(double X,double Y) c,(double X,double Y) d)
    {
        static double Cross((double X,double Y) p,(double X,double Y) q,(double X,double Y) r)=>(q.X-p.X)*(r.Y-p.Y)-(q.Y-p.Y)*(r.X-p.X);
        var c1=Cross(a,b,c); var c2=Cross(a,b,d); var c3=Cross(c,d,a); var c4=Cross(c,d,b);
        return c1*c2<=0 && c3*c4<=0;
    }
    private static decimal ReadMm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        var m=Regex.Match(value, @"^\s*([-+]?\d*\.?\d+)\s*mm\s*$", RegexOptions.IgnoreCase);
        return m.Success && decimal.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }
    private static string F(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
